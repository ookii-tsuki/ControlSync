using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SIPSorceryMedia.Abstractions;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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

        static bool initilized = false;
        static Factory1 factory;
        static Adapter1 adapter;
        static Device device;
        static Output output;
        static Output1 output1;
        static int streamWidth;
        static int streamHeight;
        static OutputDuplication duplicatedOutput;
        static OutputDescription description;

        private static void Init()
        {
            initilized = true;

            // Create DXGI Factory1
            factory = new Factory1();
            adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            device = new Device(adapter, DeviceCreationFlags.BgraSupport);

            // Get DXGI.Output
            output = adapter.GetOutput(numOutput);
            output1 = output.QueryInterface<Output1>();

            description = output1.Description;

            streamWidth = 1280;
            streamHeight = 720;

            // Duplicate the output
            duplicatedOutput = output1.DuplicateOutput(device);
        }
        public static void SendScreenBufferToServer()
        {
            if (!Client.isHost) return;


            if (!initilized)
                Init();

            if (HostPeer.VideoEncoder == null)
                return;

            var bitmap = GetScreenShot();

            if (bitmap == null) return;


            int width = bitmap.Width;
            int height = bitmap.Height;
            int stride = width * 3; // 3 bytes per pixel in RGB format
            byte[] rgbBuffer = new byte[height * stride];

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, rgbBuffer, 0, rgbBuffer.Length);
            bitmap.UnlockBits(bitmapData);

            bitmap.Dispose();

            try
            {
                HostPeer.VideoEncoder?.ExternalVideoSourceRawSample(30, width, height, rgbBuffer, VideoPixelFormatsEnum.Rgb);
            }
            catch { }
        }

        private static System.Drawing.Bitmap GetScreenShot()
        {


            try
            {
                var s = Stopwatch.StartNew();
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
                    Format = Format.B8G8R8A8_UNorm,
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
                    Format = Format.B8G8R8A8_UNorm,
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

                // Create Drawing.Bitmap
                var bitmap = new System.Drawing.Bitmap(streamWidth, streamHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var boundsRect = new Rectangle(0, 0, streamWidth, streamHeight);

                // Copy pixels from screen capture Texture to GDI bitmap
                var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                var sourcePtr = mapSource.DataPointer;
                var destPtr = mapDest.Scan0;
                for (int y = 0; y < streamHeight; y++)
                {
                    // Copy a single line 
                    Utilities.CopyMemory(destPtr, sourcePtr, (streamWidth) * 4);

                    // Advance pointers
                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.Pitch);
                    destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                }

                // Release source and dest locks
                bitmap.UnlockBits(mapDest);
                bitmap1.Unmap();
                screenResource.Dispose();
                duplicatedOutput.ReleaseFrame();

                s.Stop();

                return bitmap;


            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    throw e;
                }
            }


            return null;
        }


    }

}
