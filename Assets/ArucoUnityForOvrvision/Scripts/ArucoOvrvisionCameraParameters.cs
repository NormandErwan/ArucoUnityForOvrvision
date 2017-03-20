using System.Runtime.InteropServices;
using UnityEngine;

namespace ArucoUnity
{
  namespace Ovrvision
  {
    public class ArucoOvrvisionCameraParameters : MonoBehaviour
    {
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

      // Constants

      protected const int EXPOSURE_MIN = 0;
      protected const int EXPOSURE_MAX = 32767;
      protected const int EXPOSURE_PER_SECOND_MIN = 25;
      protected const int EXPOSURE_PER_SECOND_MAX = 240;
      protected const int GAIN_MIN = 0;
      protected const int GAIN_MAX = 47;
      protected const int BLACK_LIGHT_COMPENSATION_MIN = 0;
      protected const int BLACK_LIGHT_COMPENSATION_MAX = 1023;
      protected const int WHITE_BALANCE_RED_MIN = 0;
      protected const int WHITE_BALANCE_RED_MAX = 4095;
      protected const int WHITE_BALANCE_GREEN_MIN = 0;
      protected const int WHITE_BALANCE_GREEN_MAX = 4095;
      protected const int WHITE_BALANCE_BLUE_MIN = 0;
      protected const int WHITE_BALANCE_BLUE_MAX = 4095;

      // Editor fields

      [SerializeField]
      private bool setParametersAtStart = false;

      [SerializeField]
      [Range(EXPOSURE_MIN, EXPOSURE_MAX)]
      private int exposure = 12960;

      [SerializeField]
      [Range(EXPOSURE_PER_SECOND_MIN, EXPOSURE_PER_SECOND_MAX)]
      private int exposuresPerSecond;

      [SerializeField]
      [Range(GAIN_MIN, GAIN_MAX)]
      private int gain = 8;

      [SerializeField]
      [Range(BLACK_LIGHT_COMPENSATION_MIN, BLACK_LIGHT_COMPENSATION_MAX)]
      private int blacklightCompensation = 32;

      [SerializeField]
      private bool whiteBalanceAuto = true;

      [SerializeField]
      [Range(WHITE_BALANCE_RED_MIN, WHITE_BALANCE_RED_MAX)]
      private int whiteBalanceRed = 1474;

      [SerializeField]
      [Range(WHITE_BALANCE_GREEN_MIN, WHITE_BALANCE_GREEN_MAX)]
      private int whiteBalanceGreen = 1024;

      [SerializeField]
      [Range(WHITE_BALANCE_BLUE_MIN, WHITE_BALANCE_BLUE_MAX)]
      private int whiteBalanceBlue = 1738;

      // Properties

      public ArucoOvrvisionCamera ArucoOvrvisionCamera { get; set; }

      public bool SetParametersAtStart { get { return setParametersAtStart; } set { setParametersAtStart = value; } }

      public int Exposure
      {
        get { return exposure; }
        set { exposure = Mathf.Clamp(value, EXPOSURE_MIN, EXPOSURE_MAX); }
      }

      public int ExposuresPerSecond
      {
        get { return exposuresPerSecond; }
        set { exposuresPerSecond = Mathf.Clamp(value, EXPOSURE_PER_SECOND_MIN, EXPOSURE_PER_SECOND_MAX); }
      }

      public int Gain
      {
        get { return gain; }
        set { gain = Mathf.Clamp(value, GAIN_MIN, GAIN_MAX); }
      }

      public int BlackLightCompensation
      {
        get { return blacklightCompensation; }
        set { blacklightCompensation = Mathf.Clamp(value, BLACK_LIGHT_COMPENSATION_MIN, BLACK_LIGHT_COMPENSATION_MAX); }
      }

      public bool WhiteBalanceAuto { get { return whiteBalanceAuto; } set { whiteBalanceAuto = value; } }

      public int WhiteBalanceRed
      {
        get { return whiteBalanceRed; }
        set { whiteBalanceRed = Mathf.Clamp(value, WHITE_BALANCE_RED_MIN, WHITE_BALANCE_RED_MAX); }
      }

      public int WhiteBalanceGreen
      {
        get { return whiteBalanceGreen; }
        set { whiteBalanceGreen = Mathf.Clamp(value, WHITE_BALANCE_GREEN_MIN, WHITE_BALANCE_GREEN_MAX); }
      }

      public int WhiteBalanceBlue
      {
        get { return whiteBalanceBlue; }
        set { whiteBalanceBlue = Mathf.Clamp(value, WHITE_BALANCE_BLUE_MIN, WHITE_BALANCE_BLUE_MAX); }
      }

      // Methods

      public void GetParametersFromCamera()
      {
        if (ArucoOvrvisionCamera != null && ArucoOvrvisionCamera.IsStarted)
        {
          exposure = ovGetExposure();
          exposuresPerSecond = GetCameraModeExposureFactor(ArucoOvrvisionCamera.CameraMode) / exposure;
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
        if (ArucoOvrvisionCamera != null && ArucoOvrvisionCamera.IsStarted)
        {
          int currentExposure = ovGetExposure();
          int currentExposuresPerSecond = GetCameraModeExposureFactor(ArucoOvrvisionCamera.CameraMode) / exposure;
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

      protected void OnValidate()
      {
        SetParametersToCamera();
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