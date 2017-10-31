using ArucoUnity.Cameras.Parameters;
using ArucoUnity.Plugin;
using UnityEngine;

namespace ArucoUnity.Ovrvision
{
  public class ArucoOvrvisionCameraEye : MonoBehaviour
  {
    // Editor fields

    [SerializeField]
    private GameObject cameraParent;

    [SerializeField]
    private new Camera camera;

    [SerializeField]
    private GameObject cameraImagePlane;

    // Properties

    public GameObject CameraParent { get { return cameraParent; } set { cameraParent = value; } }

    public Camera Camera { get { return camera; } set { camera = value; } }

    public GameObject CameraImagePlane { get { return cameraImagePlane; } set { cameraImagePlane = value; } }

    // Methods

    /// <summary>
    /// Configure the cameras and the plane to display on a VR device the textures.
    /// </summary>
    public void Configure(int cameraId, CameraParameters cameraParameters, Texture2D imageTexture, float imageRatio)
    {
      float cameraFy = imageTexture.height / (2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2f));

      // Configure the camera planes facing the camera
      float cameraImagePlaneDistance = cameraFy * imageRatio / camera.aspect; // Adapt the image size to the device screen
      CameraImagePlane.GetComponent<Renderer>().material.mainTexture = imageTexture;
      CameraImagePlane.transform.localPosition = new Vector3(0, 0, cameraImagePlaneDistance);
      CameraImagePlane.transform.localScale = new Vector3(imageTexture.width, imageTexture.height, 1.0f);

      // Configure the camera
      float farClipPlaneNewValueFactor = 1.01f; // To be sure that the camera plane is visible by the camera
      camera.farClipPlane = cameraImagePlaneDistance * farClipPlaneNewValueFactor;

      // Camera parameters
      if (cameraParameters != null && cameraParameters.StereoCameraParametersList.Length > 0)
      {
        // Place one camera to match the stereo camera parameters
        StereoCameraParameters stereoCameraParameters = cameraParameters.StereoCameraParametersList[0];
        if (stereoCameraParameters.CameraIds[0] == cameraId)
        {
          cameraParent.transform.localPosition = stereoCameraParameters.TranslationVector.ToPosition();
          cameraParent.transform.localRotation = stereoCameraParameters.RotationVector.ToRotation();
        }

        // Compute the new camera matrix for rectification
        stereoCameraParameters.NewCameraMatrices[cameraId] = new Cv.Mat(3, 3, Cv.Type.CV_64F,
          new double[9] { cameraFy, 0, imageTexture.width / 2, 0, cameraFy, imageTexture.height / 2, 0, 0, 1 })
          .Clone();
      }
    }
  }
}