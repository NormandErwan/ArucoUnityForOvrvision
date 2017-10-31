using ArucoUnity.Cameras;
using ArucoUnity.Cameras.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

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

  public class ArucoOvrvisionCamera : ArucoCamera
  {
    // Constants

    protected const ProcessingMode processingMode = ProcessingMode.Demosaic;
    protected const int ovrvisionLocationId = 0;
    protected const float ovrvisionArSize = 1f;
    protected const int leftCameraId = 0;
    protected const int rightCameraId = 1;

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

    [SerializeField]
    [Tooltip("The file path to load the camera parameters.")]
    private string cameraParametersFilePath;

    [SerializeField]
    private ArucoOvrvisionCameraEye leftCameraEye;

    [SerializeField]
    private ArucoOvrvisionCameraEye rightCameraEye;

    // ArucoCamera properties implementation

    /// <summary>
    /// <see cref="ArucoCamera.CamerasNumber"/>
    /// </summary>
    public override int CameraNumber { get { return 2; } protected set { } }

    /// <summary>
    /// <see cref="ArucoCamera.Name"/>
    /// </summary>
    public override string Name { get { return "Ovrvision"; } protected set { } }

    /// <summary>
    /// <see cref="ArucoCamera.ImageRatios"/>
    /// </summary>
    public override float[] ImageRatios
    {
      get
      {
        return new float[]
        {
            (ImageTextures[leftCameraId] != null) ? ImageTextures[leftCameraId].width / (float)ImageTextures[leftCameraId].height : 0,
            (ImageTextures[rightCameraId] != null) ? ImageTextures[rightCameraId].width / (float)ImageTextures[rightCameraId].height : 0
        };
      }
    }

    // Properties

    /// <summary>
    /// The mode to use with the Ovrvision cameras.
    /// </summary>
    public CameraMode CameraMode { get { return cameraMode; } set { cameraMode = value; } }

    /// <summary>
    /// The file path to load the camera parameters.
    /// </summary>
    public string CameraParametersFilePath { get { return cameraParametersFilePath; } set { cameraParametersFilePath = value; } }

    public ArucoOvrvisionCameraEye LeftCameraEye { get { return leftCameraEye; } set { leftCameraEye = value; } }

    public ArucoOvrvisionCameraEye RightCameraEye { get { return rightCameraEye; } set { rightCameraEye = value; } }

    // Variables

    protected List<ArucoOvrvisionCameraEye> cameraEyes;
    protected byte[][] imageCapturedDatas;
    protected bool newImagesCaptured;
    protected Thread imageCaptureThread;
    protected Mutex imageCaptureMutex;
    protected Exception imageCaptureException;

    // MonoBehaviour methods

    /// <summary>
    /// Initialize the properties and the internal image capturing thread.
    /// </summary>
    protected override void Awake()
    {
      base.Awake();

      // Initialize cameraEyes
      cameraEyes = new List<ArucoOvrvisionCameraEye>(new ArucoOvrvisionCameraEye[] { LeftCameraEye, RightCameraEye });

      // Initialize the image correct orientation
      flipHorizontallyImages = false;
      flipVerticallyImages = true;

      // Initialize the internal image capturing thread
      imageCapturedDatas = new byte[CameraNumber][];
      imageCaptureMutex = new Mutex();
      imageCaptureThread = new Thread(() =>
      {
        try
        {
          while (IsConfigured && IsStarted)
          {
            imageCaptureMutex.WaitOne();
            CaptureNewImages();
            imageCaptureMutex.ReleaseMutex();
          }
        }
        catch (Exception e)
        {
          imageCaptureException = e;
          imageCaptureMutex.ReleaseMutex();
        }
      });
    }

    /// <summary>
    /// Subscribes to events.
    /// </summary>
    protected override void Start()
    {
      base.Started += ArucoOvrvisionCamera_Started;
      base.Start();
    }

    /// <summary>
    /// Unsubscribes from events.
    /// </summary>
    protected override void OnDestroy()
    {
      base.Started -= ArucoOvrvisionCamera_Started;
      base.OnDestroy();
    }

    // ArucoCamera methods

    /// <summary>
    /// Configure the VR input tracking, the Ovrvision plugin, and auto-start the cameras. The cameras need to be stopped before configured.
    /// </summary>
    public override void Configure()
    {
      if (IsStarted)
      {
        return;
      }

      // Reset state
      IsConfigured = false;
      UnityEngine.XR.InputTracking.Recenter();

      // Try to load the camera parameters
      if (CameraParametersFilePath != null && CameraParametersFilePath.Length > 0)
      {
        string fullCameraParametersFilePath = Path.Combine((Application.isEditor) ? Application.dataPath : Application.persistentDataPath, CameraParametersFilePath);
        CameraParameters = CameraParameters.LoadFromXmlFile(fullCameraParametersFilePath);
      }

      base.Configure();
    }

    /// <summary>
    /// Start the cameras and configure the images display.
    /// </summary>
    public override void StartCameras()
    {
      if (!IsConfigured || IsStarted)
      {
        return;
      }

      // Open the cameras
      if (ovOpen(ovrvisionLocationId, ovrvisionArSize, (int)CameraMode) != 0)
      {
        throw new Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
      }
      ovSetCamSyncMode(false);

      // Configure the camera textures and the camera planes
      int width = ovGetImageWidth(), height = ovGetImageHeight();
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        ImageTextures[cameraId] = new Texture2D(width, height, TextureFormat.RGB24, false);
        ImageTextures[cameraId].wrapMode = TextureWrapMode.Clamp;

        if (DisplayImages)
        {
          ImageCameras[cameraId] = cameraEyes[cameraId].Camera;
          cameraEyes[cameraId].Configure(cameraId, CameraParameters, ImageTextures[cameraId], ImageRatios[cameraId]);
        }
      }

      OnStarted();
    }

    private void ArucoOvrvisionCamera_Started()
    {
      // Initialize the image datas of the capturing thread
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        imageCapturedDatas[cameraId] = new byte[ImageDataSizes[cameraId]];
      }

      // Start the image capturing thread
      newImagesCaptured = false;
      imageCaptureThread.Start();
    }

    /// <summary>
    /// Stop the cameras.
    /// </summary>
    public override void StopCameras()
    {
      if (!IsConfigured || !IsStarted)
      {
        return;
      }

      IsStarted = false; // This flag stops the image capturing thread

      imageCaptureMutex.WaitOne();
      if (ovClose() != 0)
      {
        throw new Exception("Unkown error when closing Ovrvision cameras. Try to restart the application.");
      }
      imageCaptureMutex.ReleaseMutex();

      OnStopped();
    }

    /// <summary>
    /// Check if there was an exception in the capturing frame. If not, retrieve the frames to <see cref="ImageDatas"/>.
    /// </summary>
    protected override void UpdateCameraImages()
    {
      bool callOnImagesUpdated = false;

      // Stop if exception in the capture image thread
      if (imageCaptureException != null)
      {
        OnStopped();
        throw imageCaptureException;
      }

      // Copy frame if there were new one and no exception
      imageCaptureMutex.WaitOne();
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
    /// Get the current frame from the ovrvision cameras in the capturing thread.
    /// </summary>
    protected void CaptureNewImages()
    {
      if (!newImagesCaptured)
      {
        ovPreStoreCamData((int)processingMode);
        for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
        {
          ovGetCamImageRGB(imageCapturedDatas[cameraId], cameraId);
        }
        newImagesCaptured = true;
      }
    }
  }
}
