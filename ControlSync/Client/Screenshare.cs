﻿using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SIPSorceryMedia.Abstractions;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static SharpDX.Utilities;
using System.Windows.Media.Media3D;
using D2D = SharpDX.Direct2D1;
using Device = SharpDX.Direct3D11.Device;
using Factory1 = SharpDX.DXGI.Factory1;

namespace ControlSync.Client
{
    public static class Screenshare
    {
        public static bool downscale = true;
        // # of graphics card adapter
        const int numAdapter = 0;

        // # of output device (i.e. monitor)
        const int numOutput = 0;

        static bool closed = false;
        static Factory1 factory;
        static D2D.Factory1 d2dFactory;
        static Adapter1 adapter;
        static Device device;
        static SharpDX.DXGI.Device dxgiDevice;
        static D2D.Device d2dDevice;
        static D2D.DeviceContext frameDc;
        static Output output;
        static Output1 output1;
        static int streamWidth;
        static int streamHeight;
        static OutputDuplication duplicatedOutput;
        static Texture2D screenTexture;

        static Thread screenshareThread;
        /// <summary>
        /// Initializes the screen capture and encoding components.
        /// </summary>
        public static void Start()
        {
            if (!Client.isHost)
                return;

            // Create DXGI Factory1
            factory = new Factory1();
            adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            device = new Device(adapter, DeviceCreationFlags.BgraSupport);

            // Get DXGI.Output
            output = adapter.GetOutput(numOutput);
            output1 = output.QueryInterface<Output1>();


            streamWidth = 1280;
            streamHeight = 720;

            // Duplicate the output
            duplicatedOutput = output1.DuplicateOutput(device);

            dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>();

            d2dFactory = new D2D.Factory1();

            d2dDevice = new D2D.Device(d2dFactory, dxgiDevice);

            frameDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations);


            // Width/Height of desktop to capture
            if (!downscale)
            {
                streamWidth = output.Description.DesktopBounds.Right;
                streamHeight = output.Description.DesktopBounds.Bottom;
            }

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            screenTexture = new Texture2D(device, textureDesc);


            screenshareThread = new Thread(SendScreenBuffer);

            closed = false;

            screenshareThread.Start();
        }

        /// <summary>
        /// Closes the screen capture and encoding components.
        /// </summary>
        public static void Close()
        {
            closed = true;
            factory.Dispose();
            adapter.Dispose();
            device.Dispose();
            output.Dispose();
            output1.Dispose();
            duplicatedOutput.Dispose();
        }
        /// <summary>
        /// This method is called on a separate thread to capture the screen and send it to the remote peer.
        /// </summary>
        private static void SendScreenBuffer()
        {
            while (!closed)
            {

                if (HostPeer.VideoEncoder == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                byte[] rgbBuffer = GetScreenShot();

                if (rgbBuffer == null) continue;


                try
                {
                    HostPeer.VideoEncoder?.ExternalVideoSourceRawSample(16, streamWidth, streamHeight, rgbBuffer, VideoPixelFormatsEnum.Rgb);
                }
                catch { }

                Thread.Sleep(1);
            }
        }


        private static byte[] GetScreenShot()
        {

            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time
                duplicatedOutput.TryAcquireNextFrame(5, out duplicateFrameInformation, out screenResource);

                if (screenResource == null)
                    return null;



                byte[] rgbBuffer = null;

                if (downscale)
                    rgbBuffer = ToByteArrayAndDownscale(screenResource);
                else
                    rgbBuffer = ToByteArrayNoDownscale(screenResource);



                try
                {                    
                    screenResource.Dispose();
                    duplicatedOutput.ReleaseFrame();
                }
                catch { }


                return rgbBuffer;


            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    ClientPg.Log(e.Message);
                }
            }


            return null;
        }

        private static byte[] ToByteArrayNoDownscale(SharpDX.DXGI.Resource screenResource)
        {
            // copy resource into memory that can be accessed by the CPU
            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

            // Get the desktop capture texture
            var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

            // Allocate an RGB buffer
            int width = streamWidth;
            int height = streamHeight;
            int stride = width * 3; // 3 bytes per pixel in RGB format
            byte[] rgbBuffer = new byte[height * stride];

            // Copy the data from the texture to the RGB buffer
            unsafe
            {
                byte* sourcePtr = (byte*)mapSource.DataPointer;
                Parallel.For(0, height, y =>
                {
                    int yStride = y * stride;
                    int yPitch = y * mapSource.RowPitch;
                    for (int x = 0; x < width; x++)
                    {
                        int x4 = x * 4;
                        int x3 = x * 3;
                        rgbBuffer[yStride + x3] = sourcePtr[yPitch + x4]; // Red
                        rgbBuffer[yStride + x3 + 1] = sourcePtr[yPitch + x4 + 1]; // Green
                        rgbBuffer[yStride + x3 + 2] = sourcePtr[yPitch + x4 + 2]; // Blue
                    }
                });
            }

            device.ImmediateContext.UnmapSubresource(screenTexture, 0);

            return rgbBuffer;
        }

        private static byte[] ToByteArrayAndDownscale(SharpDX.DXGI.Resource screenResource)
        {
            using var frameSurface = screenResource.QueryInterface<Surface>();
            using var frameBitmap = new D2D.Bitmap1(frameDc, frameSurface);


            // create a GPU resized texture/surface/bitmap
            var desc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None, // only GPU
                BindFlags = BindFlags.RenderTarget, // to use D2D
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            using var texture = new Texture2D(device, desc);

            using var textureDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations); // create a D2D device context
            using var textureSurface = texture.QueryInterface<Surface>(); // this texture is a DXGI surface
            using var textureBitmap = new D2D.Bitmap1(textureDc, textureSurface); // we can create a GPU bitmap on a DXGI surface

            // associate the DC with the GPU texture/surface/bitmap
            textureDc.Target = textureBitmap;

            // this is were we draw on the GPU texture/surface
            textureDc.BeginDraw();

            // this will automatically resize
            textureDc.DrawBitmap(
                frameBitmap,
                new RawRectangleF(0, 0, streamWidth, streamHeight),
                1,
                D2D.InterpolationMode.Linear, // change this for quality vs speed
                null,
                null);

            // commit draw
            textureDc.EndDraw();


            using var texture2dc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations); // create a D2D device context

            var desc2 = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read, // only GPU
                BindFlags = BindFlags.None, // to use D2D
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = streamWidth,
                Height = streamHeight,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            using var texture2 = new Texture2D(device, desc2);

            using var texture2Surface = texture2.QueryInterface<Surface>();
            using Bitmap1 bitmap1 = new Bitmap1(texture2dc, texture2Surface);

            bitmap1.CopyFromBitmap(textureBitmap);
            // Get the desktop capture texture
            var mapSource = bitmap1.Map(MapOptions.Read);

            // Allocate an RGB buffer
            int width = desc2.Width;
            int height = desc2.Height;
            int stride = width * 3; // 3 bytes per pixel in RGB format
            byte[] rgbBuffer = new byte[height * stride];

            // Copy the data from the texture to the RGB buffer
            unsafe
            {
                byte* sourcePtr = (byte*)mapSource.DataPointer;
                Parallel.For(0, height, y =>
                {
                    int yStride = y * stride;
                    int yPitch = y * mapSource.Pitch;
                    for (int x = 0; x < width; x++)
                    {
                        int x4 = x * 4;
                        int x3 = x * 3;
                        rgbBuffer[yStride + x3] = sourcePtr[yPitch + x4]; // Red
                        rgbBuffer[yStride + x3 + 1] = sourcePtr[yPitch + x4 + 1]; // Green
                        rgbBuffer[yStride + x3 + 2] = sourcePtr[yPitch + x4 + 2]; // Blue
                    }
                });
            }

            try
            {
                bitmap1.Unmap();
            }

            catch { }

            return rgbBuffer;
        }

    }

}
