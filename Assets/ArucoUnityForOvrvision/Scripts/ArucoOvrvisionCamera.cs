using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ArucoUnity
{
  namespace Ovrvision
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
    
    public class ArucoOvrvisionCamera : Cameras.ArucoCamera
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

      protected byte[] imageData;
      protected GameObject[] cameraPlanes;

      // MonoBehaviour methods

      /// <summary>
      /// <see cref="ArucoCamera.Awake"/>
      /// </summary>
      protected override void Awake()
      {
        base.Awake();

        ImageCameras = new Camera[CameraNumber];
        ImageTextures = new Texture2D[CameraNumber];
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

        // Update VR tracking
        UnityEngine.VR.InputTracking.Recenter();

        // Try to load the camera parameters
        if (CameraParametersFilePath != null && CameraParametersFilePath.Length > 0)
        {
          string fullCameraParametersFilePath = Path.Combine((Application.isEditor) ? Application.dataPath : Application.persistentDataPath, CameraParametersFilePath);
          CameraParameters = Cameras.CameraParameters.LoadFromXmlFile(fullCameraParametersFilePath);
        }

        // Configure the image correct orientation
        flipHorizontallyImages = false;
        flipVerticallyImages = true;

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

        // Update state
        IsStarted = true;

        // Configure the cameras textures and planes
        ConfigureCameraTextures();
        if (DisplayImages)
        {
          ConfigureCamerasPlanes();
        }

        // Call observers
        OnStarted();
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

        if (ovClose() != 0)
        {
          throw new Exception("Unkown error when closing Ovrvision cameras. Try to restart the application.");
        }
        IsStarted = false;
        OnStopped();
      }

      /// <summary>
      /// Get the current frame from the ovrvision plugin and update the textures.
      /// </summary>
      protected override void UpdateCameraImages()
      {
        if (!IsConfigured || !IsStarted)
        {
          return;
        }

        ovPreStoreCamData((int)processingMode);
        for (int i = 0; i < CameraNumber; i++)
        {
          ovGetCamImageRGB(imageData, i);
          ImageTextures[i].LoadRawTextureData(imageData);
        }

        OnImagesUpdated();
      }

      // Methods

      /// <summary>
      /// Create the textures of the cameras' images and the images' data buffer.
      /// </summary>
      protected void ConfigureCameraTextures()
      {
        int imageWidth  = ovGetImageWidth(),
              imageHeight = ovGetImageHeight();

        for (int i = 0; i < CameraNumber; i++)
        {
          ImageTextures[i] = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
          ImageTextures[i].wrapMode = TextureWrapMode.Clamp;
        }

        int pixelSize = 3;
        int imageDataSize = imageWidth * imageHeight * pixelSize;
        imageData = new byte[imageDataSize];
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

        for (int cameraId = 0; cameraId < CameraNumber; cameraId++)
        {
          // Use the image texture's width as a default value if there is no camera parameters
          float CameraPlaneDistance = (CameraParameters != null) ? CameraParameters.CameraFocalLengths[cameraId].y : ImageTextures[cameraId].width;

          // Initialize rendering cameras
          if (ImageCameras[cameraId] == null)
          {
            GameObject camera = (cameraId == leftCameraId) ? new GameObject("LeftEyeCamera") : new GameObject("RightEyeCamera");
            camera.transform.SetParent(this.transform);

            ImageCameras[cameraId] = camera.AddComponent<Camera>();
            ImageCameras[cameraId].orthographic = false;
            ImageCameras[cameraId].clearFlags = CameraClearFlags.SolidColor;
            ImageCameras[cameraId].backgroundColor = Color.black;

            ImageCameras[cameraId].stereoTargetEye = (cameraId == leftCameraId) ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right;
            ImageCameras[cameraId].cullingMask = ~(1 << ((cameraId == leftCameraId) ? rightCameraLayer : leftCameraLayer)); // Render everything except the other camera plane
          }

          // Configure rendering cameras
          float farClipPlaneNewValueFactor = 1.01f; // To be sure that the camera plane is visible by the camera
          float vFov = 2f * Mathf.Atan(0.5f * ImageTextures[cameraId].height / CameraPlaneDistance) * Mathf.Rad2Deg;
          ImageCameras[cameraId].fieldOfView = vFov;
          ImageCameras[cameraId].farClipPlane = CameraPlaneDistance * farClipPlaneNewValueFactor;
          ImageCameras[cameraId].aspect = ImageRatios[cameraId];
          ImageCameras[cameraId].transform.localPosition = Vector3.zero;
          ImageCameras[cameraId].transform.localRotation = Quaternion.identity;

          // Initialize the camera planes facing the rendering Unity cameras
          if (cameraPlanes[cameraId] == null)
          {
            cameraPlanes[cameraId] = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cameraPlanes[cameraId].name = "CameraImagePlane";
            cameraPlanes[cameraId].layer = (cameraId == leftCameraId) ? leftCameraLayer : rightCameraLayer;
            cameraPlanes[cameraId].transform.parent = ImageCameras[cameraId].transform;
            cameraPlanes[cameraId].GetComponent<Renderer>().material = Resources.Load("CameraImage") as Material;
          }

          // Initialize the camera planes facing the rendering Unity cameras
          cameraPlanes[cameraId].GetComponent<MeshFilter>().mesh = ImageMeshes[cameraId];
          cameraPlanes[cameraId].GetComponent<Renderer>().material.mainTexture = ImageTextures[cameraId];
          cameraPlanes[cameraId].transform.localPosition = new Vector3(0, 0, CameraPlaneDistance); // TODO: improve with calibration and IPD
          cameraPlanes[cameraId].transform.rotation = Quaternion.identity;
          cameraPlanes[cameraId].transform.localScale = new Vector3(ImageTextures[cameraId].width, ImageTextures[cameraId].height, 1.0f);
        }
      }
    }
  }
}