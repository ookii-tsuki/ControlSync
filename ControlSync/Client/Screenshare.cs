﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using SharpDX.Direct3D;
using System.Windows.Media;
using System.Drawing;
using System.Drawing.Imaging;
using LZ4;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using SharpDX.WIC;
using SharpDX.Direct2D1;
using SharpDX.IO;
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
        static int width;
        static int height;
        static Texture2D screenTexture, smallerTexture;
        static ShaderResourceView smallerTextureView;
        static OutputDuplication duplicatedOutput;

        public static ScreenView view; // just for now
        private static void Init()
        {
            initilized = true;

            // Create DXGI Factory1
            factory = new Factory1();
            adapter = factory.GetAdapter1(numAdapter);

            // Create device from Adapter
            device = new Device(adapter);

            // Get DXGI.Output
            output = adapter.GetOutput(numOutput);
            output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            width = 1920; //((Rectangle)output.Description.DesktopBounds).Width;
            height = 1080; // ((Rectangle)output.Description.DesktopBounds).Height;

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = width / 2,
                Height = height / 2,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };

            screenTexture = new Texture2D(device, textureDesc);

            // Create Staging texture CPU-accessible
            var smallerTextureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps,
                MipLevels = 4,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            smallerTexture = new Texture2D(device, smallerTextureDesc);
            smallerTextureView = new ShaderResourceView(device, smallerTexture);

            // Duplicate the output
            duplicatedOutput = output1.DuplicateOutput(device);
        }
        public static void SendScreenBufferToServer()
        {
            if (!Client.isHost) return;


            if (!initilized)
                Init();

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


            var compressedBuffer = LZ4Codec.Encode(rgbBuffer, 0, rgbBuffer.Length);
            //Manager.UpdateScreenView(compressedBuffer, rgbBuffer.Length);
            ClientSend.VideoBuffer(compressedBuffer, rgbBuffer.Length);
            

        }

        private static System.Drawing.Bitmap GetScreenShot()
        {


            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time
                duplicatedOutput.TryAcquireNextFrame(20, out duplicateFrameInformation, out screenResource);

                if (screenResource == null)
                    return null;

                // copy resource into memory that can be accessed by the CPU
                using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                    device.ImmediateContext.CopySubresourceRegion(screenTexture2D, 0, null, smallerTexture, 0);

                device.ImmediateContext.GenerateMips(smallerTextureView);

                device.ImmediateContext.CopySubresourceRegion(smallerTexture, 1, null, screenTexture, 0);

                // Get the desktop capture texture
                var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);

                // Create Drawing.Bitmap
                var bitmap = new System.Drawing.Bitmap(width / 2, height / 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var boundsRect = new System.Drawing.Rectangle(0, 0, width / 2, height / 2);

                // Copy pixels from screen capture Texture to GDI bitmap
                var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                var sourcePtr = mapSource.DataPointer;
                var destPtr = mapDest.Scan0;
                for (int y = 0; y < height / 2; y++)
                {
                    // Copy a single line 
                    Utilities.CopyMemory(destPtr, sourcePtr, (width / 2) * 4);

                    // Advance pointers
                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                    destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                }

                // Release source and dest locks
                bitmap.UnlockBits(mapDest);
                device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                // Save the output
                //bitmap.Save(outputFileName);
                screenResource.Dispose();
                duplicatedOutput.ReleaseFrame();

                /*Stopwatch sw = Stopwatch.StartNew();

                var wicFactory = new ImagingFactory();
                var wicBitmap = new SharpDX.WIC.Bitmap(wicFactory, width / 2, height / 2, SharpDX.WIC.PixelFormat.Format32bpp3ChannelsAlpha, BitmapCreateCacheOption.CacheOnLoad);

                var btmpData = bitmap.LockBits(boundsRect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                unsafe
                {
                    byte* pGDIData = (byte*)btmpData.Scan0;

                    using (BitmapLock bl = wicBitmap.Lock(BitmapLockFlags.Write))
                    {
                        byte* pWICData = (byte*)bl.Data.DataPointer;
                        for (int y = 0; y < bitmap.Height; y++)
                        {
                            int offsetWIC = y * bl.Stride;
                            int offsetGDI = y * btmpData.Stride;
                            for (int x = 0; x < bitmap.Width; x++)
                            {
                                pWICData[offsetWIC + 0] = pGDIData[offsetGDI + 0];
                                pWICData[offsetWIC + 1] = pGDIData[offsetGDI + 1];
                                pWICData[offsetWIC + 2] = pGDIData[offsetGDI + 2];
                                pWICData[offsetWIC + 3] = pGDIData[offsetGDI + 3];
                                offsetWIC += 4;
                                offsetGDI += 4;
                            }
                        }
                    }
                }
                bitmap.UnlockBits(btmpData);

                var memoryStream = new MemoryStream();
                var wicStream = new SharpDX.WIC.WICStream(wicFactory, memoryStream);

                // Create a WIC png encoder
                var wicEncoder = new SharpDX.WIC.BitmapEncoder(wicFactory, SharpDX.WIC.ContainerFormatGuids.Jpeg);
                wicEncoder.Initialize(wicStream);

                // Create a WIC bitmap frame
                var wicFrame = new SharpDX.WIC.BitmapFrameEncode(wicEncoder);
                wicFrame.Initialize();

                // Set the frame size and pixel format
                wicFrame.SetSize(width / 2, height / 2);
                Guid guid = new Guid("6FDDC324-4E03-4BFE-B185-3D77768DC90F");
                wicFrame.SetPixelFormat(ref guid);

                // Write the bitmap data to the frame
                wicFrame.WriteSource(wicBitmap);

                // Commit the frame and the encoder
                wicFrame.Commit();
                wicEncoder.Commit();

                // Dispose the resources
                wicFrame.Dispose();
                wicEncoder.Dispose();
                wicStream.Dispose();
                wicBitmap.Dispose();
                wicFactory.Dispose();
                memoryStream.Dispose();

                sw.Stop();*/

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

        /*private static System.Drawing.Bitmap GetScreenShot()
        {


            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time
                duplicatedOutput.TryAcquireNextFrame(20, out duplicateFrameInformation, out screenResource);

                if (screenResource == null)
                    return null;
                
                    // copy resource into memory that can be accessed by the CPU
                using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                    device.ImmediateContext.CopySubresourceRegion(screenTexture2D, 0, null, smallerTexture, 0);

                device.ImmediateContext.GenerateMips(smallerTextureView);

                device.ImmediateContext.CopySubresourceRegion(smallerTexture, 1, null, screenTexture, 0);

                // Get the desktop capture texture
                var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, MapFlags.None);
                // Create Drawing.Bitmap
                var bitmap = new System.Drawing.Bitmap(width / 2, height / 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var boundsRect = new System.Drawing.Rectangle(0, 0, width / 2, height / 2);
                
                // Copy pixels from screen capture Texture to GDI bitmap
                var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                var sourcePtr = mapSource.DataPointer;
                var destPtr = mapDest.Scan0;
                for (int y = 0; y < height / 2; y++)
                {
                    // Copy a single line 
                    Utilities.CopyMemory(destPtr, sourcePtr, (width / 2) * 4);

                    // Advance pointers
                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                    destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                }

                // Release source and dest locks
                bitmap.UnlockBits(mapDest);
                device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                // Save the output
                //bitmap.Save(outputFileName);
                screenResource.Dispose();
                duplicatedOutput.ReleaseFrame();
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
        }*/


    }

}