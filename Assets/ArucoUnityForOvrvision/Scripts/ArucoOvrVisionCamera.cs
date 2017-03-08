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

    /// <summary>
    /// Manages any connected webcam to the machine, and retrieves and displays the camera's image every frame.
    /// Based on: http://answers.unity3d.com/answers/1155328/view.html
    /// </summary>
    public class ArucoOvrVisionCamera : ArucoCamera
    {
      // Constants

      protected const float DEFAULT_OVRVISION_ARSIZE = 1f;
      protected const int LEFT_CAMERA_LAYER = 24;
      protected const int RIGHT_CAMERA_LAYER = 25;

      // Ovrvision plugin functions

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovPreStoreCamData(int processingQuality);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovGetCamImageRGB(byte[] imageData, int eye);

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
          return new Vector3[] { Vector3.one, Vector3.one };
        }
      }

      // Properties

      public CameraModes CameraMode { get { return cameraMode; } set { cameraMode = value; } }

      // Variables

      protected COvrvisionUnity OvrPro = new COvrvisionUnity();

      // MonoBehaviour methods

      /// <summary>
      /// <see cref="ArucoCamera.Awake"/>
      /// </summary>
      protected override void Awake()
      {
        base.Awake();
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

        OvrPro.useOvrvisionAR = false;
        OvrPro.useOvrvisionTrack = false;
        //OvrPro.useProcessingQuality = COvrvisionUnity.OV_CAMQT_DMS; // Only demosaic, no remap

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
        if (!OvrPro.Open((int)CameraMode, DEFAULT_OVRVISION_ARSIZE))
        {
          throw new Exception("Unkown error when opening Ovrvision cameras. Try to restart the application.");
        }

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

        if (!OvrPro.Close())
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

        if (!OvrPro.camStatus)
        {
          ImagesUpdatedThisFrame = false;
          throw new Exception("Unkown error when updating the images from the Ovrvision cameras. Try to restart the application.");
        }

        ovPreStoreCamData(OvrPro.useProcessingQuality);
        for (int i = 0; i < CamerasNumber; i++)
        {
          int dataSize = OvrPro.imageSizeW * OvrPro.imageSizeH * 3;
          byte[] data = new byte[dataSize];
          ovGetCamImageRGB(data, i);
          ImageTextures[i].LoadRawTextureData(data);
          ImageTextures[i].Apply(false);
        }

          ImagesUpdatedThisFrame = true;
        OnImagesUpdated();
      }

      // Methods

      /// <summary>
      /// Create the textures of the cameras' images.
      /// </summary>
      protected void ConfigureCameraTextures()
      {
        for (int i = 0; i < CamerasNumber; i++)
        {
          ImageTextures[i] = new Texture2D(OvrPro.imageSizeW, OvrPro.imageSizeH, TextureFormat.RGB24, false);
          ImageTextures[i].wrapMode = TextureWrapMode.Clamp;
        }
      }

      /// <summary>
      /// Configure the cameras and the plane to display on a VR device the textures.
      /// </summary>
      protected void ConfigureCamerasPlanes()
      {
        for (int i = 0; i < CamerasNumber; i++)
        {
          // Configure camera
          GameObject camera = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? new GameObject("LeftEyeCamera") : new GameObject("RightEyeCamera");
          camera.transform.SetParent(this.transform);
          camera.transform.localPosition = Vector3.zero;
          camera.transform.localRotation = Quaternion.identity;

          Camera cameraCam = camera.AddComponent<Camera>();
          cameraCam.orthographic = false;
          cameraCam.clearFlags = CameraClearFlags.SolidColor;
          cameraCam.backgroundColor = Color.black;
          cameraCam.fieldOfView = 100f; // TODO: improve with calibration
          cameraCam.stereoTargetEye = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right;
          cameraCam.cullingMask = ~(1 << ((i == COvrvisionUnity.OV_CAMEYE_LEFT) ? RIGHT_CAMERA_LAYER : LEFT_CAMERA_LAYER)); // Render everythin except the other camera place

          // Configure camera plane
          GameObject cameraPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
          cameraPlane.name = "CameraImagePlane";
          cameraPlane.layer = (i == COvrvisionUnity.OV_CAMEYE_LEFT) ? LEFT_CAMERA_LAYER : RIGHT_CAMERA_LAYER;
          cameraPlane.GetComponent<Renderer>().material = Resources.Load("CameraImage") as Material;
          cameraPlane.GetComponent<MeshFilter>().mesh = ImageMeshes[i];
          cameraPlane.GetComponent<Renderer>().material.mainTexture = ImageTextures[i];
          cameraPlane.transform.parent = camera.transform;
          cameraPlane.transform.localScale = new Vector3(OvrPro.aspectW, -OvrPro.aspectH, 1.0f);
          cameraPlane.transform.localPosition =  // TODO: improve with calibration and IPD
            (i == COvrvisionUnity.OV_CAMEYE_LEFT)
            ? new Vector3(-0.032f, 0.0f, OvrPro.GetFloatPoint() + 0.02f)
            : new Vector3(OvrPro.HMDCameraRightGap().x - 0.040f, 0.0f, OvrPro.GetFloatPoint() + 0.02f);
        }
      }
    }
  }
}