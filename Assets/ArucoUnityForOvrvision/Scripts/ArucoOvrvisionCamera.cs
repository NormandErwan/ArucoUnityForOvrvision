using ArucoUnity.Cameras;
using ArucoUnity.Cameras.Parameters;
using ArucoUnity.Plugin;
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
    protected const int leftCameraLayer = 24;
    protected const int rightCameraLayer = 25;
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
    private CameraMode cameraMode = CameraMode.VR_960x950_60FPS;

    [SerializeField]
    [Tooltip("The file path to load the camera parameters.")]
    private string cameraParametersFilePath;

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
            ImageTextures[leftCameraId].width / (float)ImageTextures[leftCameraId].height,
            ImageTextures[rightCameraId].width / (float)ImageTextures[rightCameraId].height
        };
      }
    }

    /// <summary>
    /// <see cref="ArucoCamera.ImageMeshes"/>
    /// </summary>
    public override Mesh[] ImageMeshes
    {
      get
      {
        Mesh imageMesh = new Mesh();

        imageMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3( 0.5f,  0.5f, 0.0f),
            new Vector3( 0.5f, -0.5f, 0.0f),
            new Vector3(-0.5f,  0.5f, 0.0f)
        };
        imageMesh.triangles = new int[]
        {
            0, 1, 2,
            1, 0, 3
        };
        imageMesh.uv = new Vector2[]
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 1.0f)
        };
        imageMesh.RecalculateNormals();

        return new Mesh[] { imageMesh, imageMesh };
      }
    }

    // Properties

    public CameraMode CameraMode { get { return cameraMode; } set { cameraMode = value; } }

    /// <summary>
    /// The file path to load the camera parameters.
    /// </summary>
    public string CameraParametersFilePath { get { return cameraParametersFilePath; } set { cameraParametersFilePath = value; } }

    // Variables

    protected GameObject[] cameraPlanes;
    protected byte[][] imageCapturedDatas;
    protected bool newImagesCaptured;
    protected Thread imageCaptureThread;
    protected Mutex imageCaptureMutex;
    protected System.Exception imageCaptureException;

    // MonoBehaviour methods

    /// <summary>
    /// Initialize the properties and the internal image capturing thread.
    /// </summary>
    protected override void Awake()
    {
      base.Awake();

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
        catch (System.Exception e)
        {
          imageCaptureException = e;
          imageCaptureMutex.ReleaseMutex();
        }
      });
    }

    protected override void Start()
    {
      base.Started += ArucoOvrvisionCamera_Started;
      base.Start();
    }

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

      // Update VR tracking
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
        throw new System.Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
      }
      ovSetCamSyncMode(false);

      // Configure the camera textures and the camera planes
      int width = ovGetImageWidth(), height = ovGetImageHeight();
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        ImageTextures[cameraId] = new Texture2D(width, height, TextureFormat.RGB24, false);
        ImageTextures[cameraId].wrapMode = TextureWrapMode.Clamp;
      }
      if (DisplayImages)
      {
        ConfigureCamerasPlanes();
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
        throw new System.Exception("Unkown error when closing Ovrvision cameras. Try to restart the application.");
      }
      imageCaptureMutex.ReleaseMutex();

      OnStopped();
    }

    /// <summary>
    /// Get the current frame from the ovrvision plugin.
    /// </summary>
    protected override void UpdateCameraImages()
    {
      System.Exception captureException = null;

      imageCaptureMutex.WaitOne();

      // Check for exception in the capture image thread
      if (imageCaptureException != null)
      {
        captureException = imageCaptureException;
        imageCaptureException = null;
      }
      // Check for new frame images
      else if (newImagesCaptured)
      {
        for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
        {
          System.Array.Copy(imageCapturedDatas[cameraId], ImageDatas[cameraId], ImageDataSizes[cameraId]);
        }
        newImagesCaptured = false;
      }

      imageCaptureMutex.ReleaseMutex();

      // Stop if exception in the capture image thread
      if (captureException != null)
      {
        OnStopped();
        throw captureException;
      }
      // Execute the OnImagesUpdated if new images has been updated this frame
      else if (!newImagesCaptured)
      {
        OnImagesUpdated();
      }
    }

    // Methods

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

    /// <summary>
    /// Configure the cameras and the plane to display on a VR device the textures.
    /// </summary>
    protected void ConfigureCamerasPlanes()
    {
      if (cameraPlanes == null)
      {
        cameraPlanes = new GameObject[CameraNumber];
      }

      // Configure cameras and planes
      for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
      {
        if (ImageCameras[cameraId] == null)
        {
          // Set the transform between the two rendering Unity cameras
          GameObject cameraParent = (cameraId == leftCameraId) ? new GameObject("LeftEyeCamera") : new GameObject("RightEyeCamera");
          cameraParent.transform.SetParent(this.transform);

          if (CameraParameters != null && CameraParameters.StereoCameraParametersList.Length > 0)
          {
            StereoCameraParameters stereoCameraParameters = CameraParameters.StereoCameraParametersList[0];
            if (stereoCameraParameters.CameraIds[0] == cameraId)
            {
              cameraParent.transform.localPosition = stereoCameraParameters.TranslationVector.ToPosition();
              cameraParent.transform.localRotation = stereoCameraParameters.RotationVector.ToRotation();
            }
          }

          // Initialize the rendering cameras
          GameObject camera = new GameObject("Camera");
          camera.transform.SetParent(cameraParent.transform);
          ImageCameras[cameraId] = camera.AddComponent<Camera>();
          ImageCameras[cameraId].orthographic = false;
          ImageCameras[cameraId].clearFlags = CameraClearFlags.SolidColor;
          ImageCameras[cameraId].backgroundColor = Color.black;
          ImageCameras[cameraId].stereoTargetEye = (cameraId == leftCameraId) ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right;
          ImageCameras[cameraId].cullingMask = ~(1 << ((cameraId == leftCameraId) ? rightCameraLayer : leftCameraLayer)); // Render everything except the other camera plane
        }

        // Configure the rendering cameras
        float CameraPlaneDistance = ImageTextures[cameraId].height / (2f * Mathf.Tan(ImageCameras[cameraId].fieldOfView * Mathf.Deg2Rad / 2f));
        CameraPlaneDistance *= (ImageRatios[cameraId] / ImageCameras[cameraId].aspect); // Adapt the image size to the device screen
        float farClipPlaneNewValueFactor = 1.01f; // To be sure that the camera plane is visible by the camera
        ImageCameras[cameraId].nearClipPlane = 0.1f;
        ImageCameras[cameraId].farClipPlane = CameraPlaneDistance * farClipPlaneNewValueFactor;

        // Initialize and configure the camera planes facing the rendering cameras
        if (cameraPlanes[cameraId] == null)
        {
          cameraPlanes[cameraId] = GameObject.CreatePrimitive(PrimitiveType.Quad);
          cameraPlanes[cameraId].name = "ImagePlane";
          cameraPlanes[cameraId].layer = (cameraId == leftCameraId) ? leftCameraLayer : rightCameraLayer;
          cameraPlanes[cameraId].transform.parent = ImageCameras[cameraId].transform;
          cameraPlanes[cameraId].GetComponent<Renderer>().material = Resources.Load("CameraImage") as Material;
        }
        cameraPlanes[cameraId].GetComponent<MeshFilter>().mesh = ImageMeshes[cameraId];
        cameraPlanes[cameraId].GetComponent<Renderer>().material.mainTexture = ImageTextures[cameraId];
        cameraPlanes[cameraId].transform.localPosition = new Vector3(0, 0, CameraPlaneDistance);
        cameraPlanes[cameraId].transform.localRotation = Quaternion.identity;
        cameraPlanes[cameraId].transform.localScale = new Vector3(ImageTextures[cameraId].width, ImageTextures[cameraId].height, 1.0f);

        // Compute the new camera matrix for rectification
        if (CameraParameters != null && CameraParameters.StereoCameraParametersList.Length > 0)
        {
          float fx = ImageTextures[cameraId].height / (2f * Mathf.Tan(ImageCameras[cameraId].fieldOfView * Mathf.Deg2Rad / 2f));
          CameraParameters.StereoCameraParametersList[0].NewCameraMatrices[cameraId] = new Cv.Mat(3, 3, Cv.Type.CV_64F,
            new double[9] { fx, 0, ImageTextures[cameraId].width / 2, 0, fx, ImageTextures[cameraId].height / 2, 0, 0, 1 })
            .Clone();
        }
      }
    }
  }
}
