using ArucoUnity.Utility;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ArucoUnity
{
  namespace Ovrvision
  {
    public enum CameraModes
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

    public enum ProcessingModes
    {
      DEMOSAIC_REMAP = 0,
      DEMOSAIC,
      NONE,
    }
    
    public class ArucoOvrVisionCamera : ArucoCamera
    {
      // Constants

      protected const ProcessingModes PROCESSING_MODE = ProcessingModes.DEMOSAIC;
      protected const int OVRVISION_LOCATION_ID = 0;
      protected const float OVRVISION_ARSIZE = 1f;
      protected const int LEFT_CAMERA_LAYER = 24;
      protected const int RIGHT_CAMERA_LAYER = 25;

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

      private CameraModes cameraMode = CameraModes.VR_960x950_60FPS;

      // ArucoCamera properties implementation

      /// <summary>
      /// <see cref="ArucoCamera.CamerasNumber"/>
      /// </summary>
      public override int CamerasNumber { get { return 2; } protected set { } }

      /// <summary>
      /// <see cref="ArucoCamera.Name"/>
      /// </summary>
      public override string Name { get; protected set; }

      /// <summary>
      /// <see cref="ArucoCamera.ImageRotations"/>
      /// </summary>
      public override Quaternion[] ImageRotations
      {
        get
        {
          return new Quaternion[] { Quaternion.identity, Quaternion.identity };
        }
      }

      /// <summary>
      /// <see cref="ArucoCamera.ImageRatios"/>
      /// </summary>
      public override float[] ImageRatios
      {
        get
        {
          return new float[]
          {
            ImageTextures[COvrvisionUnity.OV_CAMEYE_LEFT].width / (float)ImageTextures[COvrvisionUnity.OV_CAMEYE_LEFT].height,
            ImageTextures[COvrvisionUnity.OV_CAMEYE_RIGHT].width / (float)ImageTextures[COvrvisionUnity.OV_CAMEYE_RIGHT].height
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

      /// <summary>
      /// <see cref="ArucoCamera.ImageUvRectFlips"/>
      /// </summary>
      public override Rect[] ImageUvRectFlips
      {
        get
        {
          Rect imageRect = new Rect(0f, 0f, 1f, 1f);
          return new Rect[] { imageRect, imageRect };
        }
      }

      /// <summary>
      /// <see cref="ArucoCamera.ImageScalesFrontFacing"/>
      /// </summary>
      public override Vector3[] ImageScalesFrontFacing
      {
        get
        {
          Vector3 imageScale = new Vector3(1f, -1f, 1f);
          return new Vector3[] { imageScale, imageScale };
        }
      }

      // Properties

      public CameraModes CameraMode { get { return cameraMode; } set { cameraMode = value; } }

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
        ImageCameras = new Camera[CamerasNumber];
        ImageTextures = new Texture2D[CamerasNumber];
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

        UnityEngine.VR.InputTracking.Recenter();

        // Update state
        IsConfigured = true;
        OnConfigured();

        // AutoStart
        if (AutoStart)
        {
          StartCameras();
        }
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
        if (ovOpen(OVRVISION_LOCATION_ID, OVRVISION_ARSIZE, (int)CameraMode) != 0)
        {
          throw new Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
        }
        ovSetCamSyncMode(false);

        // Configure the cameras textures and planes
        ConfigureCameraTextures();
        if (DisplayImages)
        {
          ConfigureCamerasPlanes();
        }

        // Update state
        IsStarted = true;
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
          ImagesUpdatedThisFrame = false;
          return;
        }

        ovPreStoreCamData((int)PROCESSING_MODE);
        for (int i = 0; i < CamerasNumber; i++)
        {
          ovGetCamImageRGB(imageData, i);
          ImageTextures[i].LoadRawTextureData(imageData);
          ImageTextures[i].Apply(false);
        }

        ImagesUpdatedThisFrame = true;
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

        for (int i = 0; i < CamerasNumber; i++)
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
          cameraPlanes = new GameObject[CamerasNumber];
        }

        for (int i = 0; i < CamerasNumber; i++)
        {
          float CameraPlaneDistance = (CameraParameters != null) ? CameraParameters[i].CameraFy : ImageTextures[i].width;

          // Initialize rendering cameras
          if (ImageCameras[i] == null)
          {
            GameObject camera = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? new GameObject("LeftEyeCamera") : new GameObject("RightEyeCamera");
            camera.transform.SetParent(this.transform);

            ImageCameras[i] = camera.AddComponent<Camera>();
            ImageCameras[i].orthographic = false;
            ImageCameras[i].clearFlags = CameraClearFlags.SolidColor;
            ImageCameras[i].backgroundColor = Color.black;

            ImageCameras[i].stereoTargetEye = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right;
            ImageCameras[i].cullingMask = ~(1 << ((i == COvrvisionUnity.OV_CAMEYE_LEFT) ? RIGHT_CAMERA_LAYER : LEFT_CAMERA_LAYER)); // Render everything except the other camera plane
          }

          // Configure rendering cameras
          float farClipPlaneNewValueFactor = 1.01f; // To be sure that the camera plane is visible by the camera
          float vFov = 2f * Mathf.Atan(0.5f * ImageTextures[i].height / CameraPlaneDistance) * Mathf.Rad2Deg;
          ImageCameras[i].fieldOfView = vFov;
          ImageCameras[i].farClipPlane = CameraPlaneDistance * farClipPlaneNewValueFactor;
          ImageCameras[i].aspect = ImageRatios[i];
          ImageCameras[i].transform.localPosition = Vector3.zero;
          ImageCameras[i].transform.localRotation = Quaternion.identity;

          // Initialize the camera planes facing the rendering Unity cameras
          if (cameraPlanes[i] == null)
          {
            cameraPlanes[i] = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cameraPlanes[i].name = "CameraImagePlane";
            cameraPlanes[i].layer = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? LEFT_CAMERA_LAYER : RIGHT_CAMERA_LAYER;
            cameraPlanes[i].transform.parent = ImageCameras[i].transform;
            cameraPlanes[i].GetComponent<Renderer>().material = Resources.Load("CameraImage") as Material;
          }

          // Initialize the camera planes facing the rendering Unity cameras
          cameraPlanes[i].GetComponent<MeshFilter>().mesh = ImageMeshes[i];
          cameraPlanes[i].GetComponent<Renderer>().material.mainTexture = ImageTextures[i];
          cameraPlanes[i].transform.localPosition = new Vector3(0, 0, CameraPlaneDistance); // TODO: improve with calibration and IPD
          cameraPlanes[i].transform.rotation = ImageRotations[i];
          cameraPlanes[i].transform.localScale = new Vector3(ImageTextures[i].width, ImageTextures[i].height, 1.0f);
          cameraPlanes[i].transform.localScale = Vector3.Scale(cameraPlanes[i].transform.localScale, ImageScalesFrontFacing[i]);
        }
      }
    }
  }
}