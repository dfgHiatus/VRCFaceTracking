﻿using System.Runtime.InteropServices;
using UnityEngine;

namespace VRCFaceTracking.Pimax
{
	public class Ai1EyeData
	{
		public EyeExpressionState Left;
		public EyeExpressionState Right;
		public EyeExpressionState Recommended;

		EyeExpressionState PrevLeft;
		EyeExpressionState PrevRight;
		EyeExpressionState PrevRecommended;

		private EyeExpressionState GetEyeExpressionState(Eye eye)
		{
			EyeExpressionState state;
			state.PupilCenterX = PimaxTracker.GetEyeExpression(eye, EyeExpression.PupilCenterX);
			state.PupilCenterY = PimaxTracker.GetEyeExpression(eye, EyeExpression.PupilCenterY);
			state.Openness = PimaxTracker.GetEyeExpression(eye, EyeExpression.Openness);

			return state;
		}

		public void Update()
		{
			float smooth = 0.8f;

			Left = GetEyeExpressionState(Eye.Left);
			Right = GetEyeExpressionState(Eye.Right);
			Recommended = GetEyeExpressionState(PimaxTracker.GetRecommendedEye());

			/* Credit to azmidi for Linear Interpolation */
			PrevLeft.PupilCenterX = Mathf.Lerp(PrevLeft.PupilCenterX, Left.PupilCenterX, smooth);
			PrevLeft.PupilCenterY = Mathf.Lerp(PrevLeft.PupilCenterY, Left.PupilCenterY, smooth);
			PrevLeft.Openness = Left.Openness;

			PrevRight.PupilCenterX = Mathf.Lerp(PrevRight.PupilCenterX, Right.PupilCenterX, smooth);
			PrevRight.PupilCenterY = Mathf.Lerp(PrevRight.PupilCenterY, Right.PupilCenterY, smooth);
			PrevRight.Openness = Right.Openness;

			PrevRecommended.PupilCenterY = Mathf.Lerp(PrevRecommended.PupilCenterY, Recommended.PupilCenterY, smooth);

			Left = FilterEye(PrevLeft);
			Right = FilterEye(PrevRight);
			Recommended = FilterEye(PrevRecommended);
		}

		// Test Filtering Helper Method
		/* Credit to azmidi for arctangent filtering */
		private static EyeExpressionState FilterEye(EyeExpressionState state)
		{
			state.Openness = 0.65f * Mathf.Atan(5f * state.Openness);
			state.PupilCenterX = 0.65f * Mathf.Atan(state.PupilCenterX);
			state.PupilCenterY = 0.65f * Mathf.Atan(state.PupilCenterY);

			return state;
		}
	}


	// Mad props to NGenesis for these bindings <3

	public enum Eye
	{
		Any,
		Left,
		Right
	}

	public enum CallbackType
	{
		Start,
		Stop,
		Update
	}


	public enum EyeExpression
	{
		PupilCenterX, // Pupil center on the X axis, smoothed and normalized between -1 (looking left) ... 0 (looking forward) ... 1 (looking right)
		PupilCenterY, // Pupil center on the Y axis, smoothed and normalized between -1 (looking up) ... 0 (looking forward) ... 1 (looking down)
		Openness, // How open the eye is, smoothed and normalized between 0 (fully closed) ... 1 (fully open)
		Blink // Blink, 0 (not blinking) or 1 (blinking)
	}

	public struct EyeExpressionState
	{
		public float PupilCenterX;
		public float PupilCenterY;
		public float Openness;
	};

	public delegate void EyeTrackerEventHandler();

	public static class PimaxTracker
	{
		/// <summary>
		/// Registers callbacks for the tracker to notify when it's finished initializing, when it has new data available and when the module is stopped.
		/// </summary>
		[DllImport("PimaxEyeTracker", EntryPoint = "RegisterCallback")] public static extern void RegisterCallback(CallbackType type, EyeTrackerEventHandler callback);

		/// <summary>
		/// Initializes the module.
		/// </summary>
		/// <returns>Initialization Successful</returns>
		[DllImport("PimaxEyeTracker", EntryPoint = "Start")] public static extern bool Start();

		/// <summary>
		/// Stops the eye tracking module and disconnects the server
		/// </summary>
		[DllImport("PimaxEyeTracker", EntryPoint = "Stop")] public static extern void Stop();
		[DllImport("PimaxEyeTracker", EntryPoint = "GetEyeExpression")] public static extern float GetEyeExpression(Eye eye, EyeExpression expression);
		[DllImport("PimaxEyeTracker", EntryPoint = "GetRecommendedEye")] public static extern Eye GetRecommendedEye();
	}
}