using ArucoUnity.Cameras;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.XR;

namespace ArucoUnity.Ovrvision
{
  public enum CameraMode
  {
    Full_2560x1920_15FPS = 0,
    FHD_1920x1080_30FPS,
    SXGAM_1280x960_45FPS,
    VR_960x950_60FPS,
    WXGA_1280x800_60FPS,
    VGA_640x480_90FPS,
    QVGA_320x240_120FPS,
    USB2_SXGAM_1280x960_15FPS,
    USB2_VGA_640x480_30FPS,
  }

  public enum ProcessingMode
  {
    DemosaicRemap = 0,
    Demosaic,
    None,
  }

  /// <summary>
  /// Captures image frames from the Ovrvision Pro stereo camera.
  /// </summary>
  public class OvrvisionArucoCamera : StereoArucoCamera
  {
    // Constants

    protected readonly ProcessingMode processingMode = ProcessingMode.Demosaic;
    protected readonly int ovrvisionLocationId = 0;
    protected readonly float ovrvisionArSize = 1f;

    // Ovrvision plugin functions

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int ovOpen(int locationID, float arSize, int mode);

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int ovClose();

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern void ovPreStoreCamData(int processingMode);

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern void ovGetCamImageRGB(byte[] imageData, int eye);

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int ovGetImageWidth();

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int ovGetImageHeight();

    [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern float ovSetCamSyncMode(bool value);

    // Editor fields

    [SerializeField]
    [Tooltip("The mode to use with the Ovrvision cameras.")]
    private CameraMode cameraMode = CameraMode.VR_960x950_60FPS;

    // IArucoCamera properties

    public override string Name { get { return "Ovrvision"; } protected set { } }

    // Properties

    /// <summary>
    /// Gets or sets the mode to use with the Ovrvision cameras.
    /// </summary>
    public CameraMode CameraMode { get { return cameraMode; } set { cameraMode = value; } }

    // Variables

    protected bool camerasOpened = false;
    protected byte[][] imageCapturedDatas;
    protected bool newImagesCaptured;
    protected Thread imagesCaptureThread;
    protected Mutex imageCaptureMutex = new Mutex();
    protected Exception imageCaptureException;

    // ArucoCamera methods

    /// <summary>
    /// Initializes the properties and recenter the VR input tracking.
    /// </summary>
    public override void Configure()
    {
      base.Configure();

      flipHorizontallyImages = false;
      flipVerticallyImages = true;

      imageCapturedDatas = new byte[CameraNumber][];

      InputTracking.Recenter();

      OnConfigured();
    }

    /// <summary>
    /// Starts the cameras and the images capturing thread.
    /// </summary>
    public override void StartController()
    {
      base.StartController();

      // Open the cameras
      if (ovOpen(ovrvisionLocationId, ovrvisionArSize, (int)CameraMode) != 0)
      {
        throw new Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
      }
      ovSetCamSyncMode(false);
      camerasOpened = true;

      // Configure the camera textures
      int width = ovGetImageWidth(), height = ovGetImageHeight();
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        ImageTextures[cameraId] = new Texture2D(width, height, TextureFormat.RGB24, false);
        ImageTextures[cameraId].wrapMode = TextureWrapMode.Clamp;
      }

      // Initialize images properties
      OnStarted();

      // Initialize the image datas of the capturing thread
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        imageCapturedDatas[cameraId] = new byte[ImageDataSizes[cameraId]];
      }

      // Start the image capturing thread
      newImagesCaptured = false;
      imagesCaptureThread = new Thread(ImagesCapturingThreadMain);
      imagesCaptureThread.Start();
    }

    /// <summary>
    /// Stops the cameras and the image capturing thread.
    /// </summary>
    public override void StopController()
    {
      if (camerasOpened)
      {
        imageCaptureMutex.WaitOne();
        if (ovClose() != 0)
        {
          throw new Exception("Unkown error when closing Ovrvision cameras. Try to restart the application.");
        }
        imageCaptureMutex.ReleaseMutex();
      }

      base.StopController();
      OnStopped();
    }

    /// <summary>
    /// Checks if there was an exception in the image capturing thread otherwise copies the captured image frame to <see cref="ImageDatas"/>.
    /// </summary>
    protected override void UpdateCameraImages()
    {
      bool callOnImagesUpdated = false;

      // Stop if exception in the capture image thread
      imageCaptureMutex.WaitOne();
      if (imageCaptureException != null)
      {
        Exception e = imageCaptureException;
        imageCaptureMutex.ReleaseMutex();

        StopController();

        throw e;
      }

      // Copy frame if there were new one and no exception
      if (newImagesCaptured)
      {
        for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
        {
          Array.Copy(imageCapturedDatas[cameraId], ImageDatas[cameraId], ImageDataSizes[cameraId]);
        }
        callOnImagesUpdated = true;
        newImagesCaptured = false;
      }
      imageCaptureMutex.ReleaseMutex();

      // Execute the OnImagesUpdated if new images has been updated this frame
      if (callOnImagesUpdated)
      {
        OnImagesUpdated();
      }
    }

    // Methods

    /// <summary>
    /// Gets the current image frame from the cameras if cameras are configured and started.
    /// </summary>
    protected void ImagesCapturingThreadMain()
    {
      try
      {
        while (IsConfigured && IsStarted)
        {
          imageCaptureMutex.WaitOne();

          if (!newImagesCaptured)
          {
            ovPreStoreCamData((int)processingMode);
            for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
            {
              ovGetCamImageRGB(imageCapturedDatas[cameraId], cameraId);
            }
            newImagesCaptured = true;
          }

          imageCaptureMutex.ReleaseMutex();
        }
      }
      catch (Exception e)
      {
        imageCaptureException = e;
        imageCaptureMutex.ReleaseMutex();
      }
    }
  }
}
