using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SIPSorceryMedia.Abstractions;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using D2D = SharpDX.Direct2D1;
using Device = SharpDX.Direct3D11.Device;
using Factory1 = SharpDX.DXGI.Factory1;

namespace ControlSync.Client
{
    public static class Screenshare
    {
        // # of graphics card adapter
        const int numAdapter = 0;

        // # of output device (i.e. monitor)
        const int numOutput = 0;

        static bool closed = false;
        static Factory1 factory;
        static Adapter1 adapter;
        static Device device;
        static Output output;
        static Output1 output1;
        static int streamWidth;
        static int streamHeight;
        static OutputDuplication duplicatedOutput;

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
                    HostPeer.VideoEncoder?.ExternalVideoSourceRawSample(20, streamWidth, streamHeight, rgbBuffer, VideoPixelFormatsEnum.Rgb);
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
                duplicatedOutput.TryAcquireNextFrame(20, out duplicateFrameInformation, out screenResource);

                if (screenResource == null)
                    return null;

                using var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>();
                using var d2dFactory = new D2D.Factory1();
                using var d2dDevice = new D2D.Device(d2dFactory, dxgiDevice);
                using var frameDc = new D2D.DeviceContext(d2dDevice, D2D.DeviceContextOptions.EnableMultithreadedOptimizations);
                using var frameSurface = screenResource.QueryInterface<Surface>();
                using var frameBitmap = new D2D.Bitmap1(frameDc, frameSurface);

                // create a GPU resized texture/surface/bitmap
                var desc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read, // only GPU
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
                    D2D.InterpolationMode.HighQualityCubic, // change this for quality vs speed
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
                var sourcePtr = mapSource.DataPointer;
                for (int y = 0; y < height; y++)
                {
                    // Copy a single line 
                    for (int x = 0; x < width; x++)
                    {
                        // Get the RGBA value from the texture
                        int rgba = Marshal.ReadInt32(sourcePtr);

                        // Extract the RGB components and store them in the buffer
                        rgbBuffer[y * stride + x * 3] = (byte)(rgba & 0xFF); // Red
                        rgbBuffer[y * stride + x * 3 + 1] = (byte)((rgba >> 8) & 0xFF); // Green
                        rgbBuffer[y * stride + x * 3 + 2] = (byte)((rgba >> 16) & 0xFF); // Blue

                        // Advance the source pointer by 4 bytes
                        sourcePtr = IntPtr.Add(sourcePtr, 4);
                    }

                    // Advance the source pointer by the pitch
                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.Pitch - width * 4);
                }

                bitmap1.Unmap();
                screenResource.Dispose();
                duplicatedOutput.ReleaseFrame();


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

    }

}
