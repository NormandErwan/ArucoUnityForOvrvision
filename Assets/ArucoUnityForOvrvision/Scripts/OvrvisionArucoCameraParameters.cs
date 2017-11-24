using System.Runtime.InteropServices;
using UnityEngine;

namespace ArucoUnity
{
  namespace Ovrvision
  {
    public class OvrvisionArucoCameraParameters : MonoBehaviour
    {
      // Constants

      protected const int exposureMin = 0;
      protected const int exposureMax = 32767;
      protected const int exposurePerSecondMin = 25;
      protected const int exposurePerSecondMax = 240;
      protected const int gainMin = 0;
      protected const int gainMax = 47;
      protected const int blackLightCompensationMin = 0;
      protected const int blackLightCompensationMax = 1023;
      protected const int whiteBalanceRedMin = 0;
      protected const int whiteBalanceRedMax = 4095;
      protected const int whiteBalanceGreenMin = 0;
      protected const int whiteBalanceGreenMax = 4095;
      protected const int whiteBalanceBlueMin = 0;
      protected const int whiteBalanceBlueMax = 4095;

      // Ovrvision plugin functions

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetExposure();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetExposure(int value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovSetExposurePerSec(float value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetGain();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetGain(int value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetBLC();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetBLC(int value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern bool ovGetWhiteBalanceAuto();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetWhiteBalanceAuto(bool value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetWhiteBalanceR();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetWhiteBalanceR(int value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetWhiteBalanceG();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetWhiteBalanceG(int value);

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern int ovGetWhiteBalanceB();

      [DllImport("ovrvision", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
      static extern void ovSetWhiteBalanceB(int value);

      // Editor fields

      [SerializeField]
      private OvrvisionArucoCamera ovrvisionArucoCamera;

      [SerializeField]
      private bool setParametersAtStart = false;

      [SerializeField]
      [Range(exposureMin, exposureMax)]
      private int exposure = 12960;

      [SerializeField]
      [Range(exposurePerSecondMin, exposurePerSecondMax)]
      private int exposuresPerSecond;

      [SerializeField]
      [Range(gainMin, gainMax)]
      private int gain = 8;

      [SerializeField]
      [Range(blackLightCompensationMin, blackLightCompensationMax)]
      private int blacklightCompensation = 32;

      [SerializeField]
      private bool whiteBalanceAuto = true;

      [SerializeField]
      [Range(whiteBalanceRedMin, whiteBalanceRedMax)]
      private int whiteBalanceRed = 1474;

      [SerializeField]
      [Range(whiteBalanceGreenMin, whiteBalanceGreenMax)]
      private int whiteBalanceGreen = 1024;

      [SerializeField]
      [Range(whiteBalanceBlueMin, whiteBalanceBlueMax)]
      private int whiteBalanceBlue = 1738;

      // Properties

      public OvrvisionArucoCamera OvrvisionArucoCamera { get { return ovrvisionArucoCamera; } set { ovrvisionArucoCamera = value; } }

      public bool SetParametersAtStart { get { return setParametersAtStart; } set { setParametersAtStart = value; } }

      public int Exposure
      {
        get { return exposure; }
        set { exposure = Mathf.Clamp(value, exposureMin, exposureMax); }
      }

      public int ExposuresPerSecond
      {
        get { return exposuresPerSecond; }
        set { exposuresPerSecond = Mathf.Clamp(value, exposurePerSecondMin, exposurePerSecondMax); }
      }

      public int Gain
      {
        get { return gain; }
        set { gain = Mathf.Clamp(value, gainMin, gainMax); }
      }

      public int BlackLightCompensation
      {
        get { return blacklightCompensation; }
        set { blacklightCompensation = Mathf.Clamp(value, blackLightCompensationMin, blackLightCompensationMax); }
      }

      public bool WhiteBalanceAuto { get { return whiteBalanceAuto; } set { whiteBalanceAuto = value; } }

      public int WhiteBalanceRed
      {
        get { return whiteBalanceRed; }
        set { whiteBalanceRed = Mathf.Clamp(value, whiteBalanceRedMin, whiteBalanceRedMax); }
      }

      public int WhiteBalanceGreen
      {
        get { return whiteBalanceGreen; }
        set { whiteBalanceGreen = Mathf.Clamp(value, whiteBalanceGreenMin, whiteBalanceGreenMax); }
      }

      public int WhiteBalanceBlue
      {
        get { return whiteBalanceBlue; }
        set { whiteBalanceBlue = Mathf.Clamp(value, whiteBalanceBlueMin, whiteBalanceBlueMax); }
      }

      // MonoBehaviour methods

      protected void Start()
      {
        if (OvrvisionArucoCamera != null)
        {
          if (OvrvisionArucoCamera.IsStarted)
          {
            ConfigureCameraParameters();
          }
          OvrvisionArucoCamera.Started += ConfigureCameraParameters;
        }
      }

      // Methods

      public void GetParametersFromCamera()
      {
        if (OvrvisionArucoCamera != null && OvrvisionArucoCamera.IsStarted)
        {
          exposure = ovGetExposure();
          exposuresPerSecond = GetCameraModeExposureFactor(OvrvisionArucoCamera.CameraMode) / exposure;
          gain = ovGetGain();
          blacklightCompensation = ovGetBLC();
          whiteBalanceAuto = ovGetWhiteBalanceAuto();
          whiteBalanceRed = ovGetWhiteBalanceR();
          whiteBalanceGreen = ovGetWhiteBalanceG();
          whiteBalanceBlue = ovGetWhiteBalanceB();
        }
      }

      public void SetParametersToCamera()
      {
        // Set the updated parameters
        if (OvrvisionArucoCamera != null && OvrvisionArucoCamera.IsStarted)
        {
          int currentExposure = ovGetExposure();
          int currentExposuresPerSecond = GetCameraModeExposureFactor(OvrvisionArucoCamera.CameraMode) / exposure;
          if (exposure != currentExposure)
          {
            ovSetExposure(exposure);
          }
          else if (currentExposuresPerSecond != exposuresPerSecond)
          {
            ovSetExposurePerSec(exposuresPerSecond);
          }

          ovSetGain(gain);
          ovSetBLC(blacklightCompensation);
          ovSetWhiteBalanceAuto(whiteBalanceAuto);
          ovSetWhiteBalanceR(whiteBalanceRed);
          ovSetWhiteBalanceG(whiteBalanceGreen);
          ovSetWhiteBalanceB(whiteBalanceBlue);
        }

        // Update the other parameters
        GetParametersFromCamera();
      }

      protected void ConfigureCameraParameters()
      {
        if (SetParametersAtStart)
        {
          SetParametersToCamera();
        }
        else
        {
          GetParametersFromCamera();
        }
      }

      protected int GetCameraModeExposureFactor(CameraMode cameraMode)
      {
        switch(cameraMode)
        {
          case CameraMode.Full_2560x1920_15FPS:
            return 480000;
          case CameraMode.FHD_1920x1080_30FPS:
            return 570580;
          case CameraMode.SXGAM_1280x960_45FPS:
            return 720113;
          case CameraMode.VR_960x950_60FPS:
            return 937157;
          case CameraMode.WXGA_1280x800_60FPS:
            return 783274;
          case CameraMode.VGA_640x480_90FPS:
            return 720113;
          case CameraMode.QVGA_320x240_120FPS:
            return 491520;
          case CameraMode.USB2_SXGAM_1280x960_15FPS:
            return 240000;
          case CameraMode.USB2_VGA_640x480_30FPS:
            return 240000;
        }
        return 0;
      }
    }
  }
}