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
  /// Captures image from the Ovrvision Pro stereo camera.
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
    [Tooltip("The camera mode to use.")]
    private CameraMode cameraMode = CameraMode.VR_960x950_60FPS;

    // IArucoCamera properties

    public override string Name { get { return "Ovrvision"; } protected set { } }

    // Properties

    /// <summary>
    /// Gets or sets the camera mode to use.
    /// </summary>
    public CameraMode CameraMode { get { return cameraMode; } set { cameraMode = value; } }

    // Variables

    protected bool newImagesCaptured, imagesCaptureThreadUpdated;
    protected Thread imagesCaptureThread;
    protected Mutex imagesCaptureMutex = new Mutex();
    protected Exception imagesCaptureException;

    // ConfigurableController methods

    /// <summary>
    /// Initializes the properties and recenter the VR input tracking.
    /// </summary>
    protected override void Configuring()
    {
      flipHorizontallyImages = false;
      flipVerticallyImages = true;

      InputTracking.Recenter();

      base.Configuring();
    }

    /// <summary>
    /// Starts the camera, configures <see cref="ArucoCamera.Textures"/> and starts the image capturing thread.
    /// </summary>
    protected override void Starting()
    {
      base.Starting();

      // Open the camera
      if (ovOpen(ovrvisionLocationId, ovrvisionArSize, (int)CameraMode) != 0)
      {
        throw new Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
      }
      ovSetCamSyncMode(false);

      // Configure the camera textures
      int width = ovGetImageWidth(), height = ovGetImageHeight();
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        Textures[cameraId] = new Texture2D(width, height, TextureFormat.RGB24, false);
      }
    }

    /// <summary>
    /// Configures and starts the image capturing thread. 
    /// </summary>
    protected override void OnStarted()
    {
      base.OnStarted();

      newImagesCaptured = false;
      imagesCaptureThread = new Thread(ImagesCapturingThreadMain);
      imagesCaptureThread.Start();
    }

    /// <summary>
    /// Stops the cameras and the image capturing thread.
    /// </summary>
    protected override void Stopping()
    {
      base.Stopping();
      imagesCaptureMutex.WaitOne();
      if (ovClose() != 0)
      {
        throw new Exception("Unkown error when closing Ovrvision cameras. Try to restart the application.");
      }
      imagesCaptureMutex.ReleaseMutex();
    }

    // ArucoCamera methods

    /// <summary>
    /// Checks if there was an exception in the image capturing thread otherwise copies the captured image frame to
    /// <see cref="ImageDatas"/>.
    /// </summary>
    protected override bool UpdatingImages()
    {
      imagesCaptureMutex.WaitOne();
      if (imagesCaptureException != null)
      {
        Exception e = imagesCaptureException;
        imagesCaptureMutex.ReleaseMutex();

        StopController();
        throw e;
      }

      imagesCaptureThreadUpdated = newImagesCaptured;
      newImagesCaptured = false;
      imagesCaptureMutex.ReleaseMutex();

      return imagesCaptureThreadUpdated;
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
          imagesCaptureMutex.WaitOne();
          if (!newImagesCaptured)
          {
            ovPreStoreCamData((int)processingMode);
            for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
            {
              ovGetCamImageRGB(NextImageDatas[cameraId], cameraId);
            }

            newImagesCaptured = true;
          }
          imagesCaptureMutex.ReleaseMutex();
        }
      }
      catch (Exception e)
      {
        imagesCaptureException = e;
        imagesCaptureMutex.ReleaseMutex();
      }
    }
  }
}
