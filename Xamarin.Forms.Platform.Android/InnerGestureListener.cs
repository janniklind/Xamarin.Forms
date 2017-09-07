using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Android.Runtime;
using Android.Views;
using Object = Java.Lang.Object;

namespace Xamarin.Forms.Platform.Android
{
	internal class InnerGestureListener : Object, GestureDetector.IOnGestureListener, GestureDetector.IOnDoubleTapListener
	{
		bool _isScrolling;		
		float _lastX;
		float _lastY;
		bool _disposed;

		Func<bool> _scrollCompleteDelegate;
		Func<float, float, int, bool> _scrollDelegate;
		Func<int, bool> _scrollStartedDelegate;
		Func<int, bool> _tapDelegate;
		Func<int, IEnumerable<TapGestureRecognizer>> _tapGestureRecognizers;

		public InnerGestureListener(Func<int, bool> tapDelegate, 
			Func<int, IEnumerable<TapGestureRecognizer>> tapGestureRecognizers, 
			Func<float, float, int, bool> scrollDelegate,
			Func<int, bool> scrollStartedDelegate, Func<bool> scrollCompleteDelegate)
		{
			if (tapDelegate == null)
				throw new ArgumentNullException(nameof(tapDelegate));
			if (tapGestureRecognizers == null)
				throw new ArgumentNullException(nameof(tapGestureRecognizers));
			if (scrollDelegate == null)
				throw new ArgumentNullException(nameof(scrollDelegate));
			if (scrollStartedDelegate == null)
				throw new ArgumentNullException(nameof(scrollStartedDelegate));
			if (scrollCompleteDelegate == null)
				throw new ArgumentNullException(nameof(scrollCompleteDelegate));

			_tapDelegate = tapDelegate;
			_tapGestureRecognizers = tapGestureRecognizers;
			_scrollDelegate = scrollDelegate;
			_scrollStartedDelegate = scrollStartedDelegate;
			_scrollCompleteDelegate = scrollCompleteDelegate;
		}

		// This is needed because GestureRecognizer callbacks can be delayed several hundred milliseconds
		// which can result in the need to resurrect this object if it has already been disposed. We dispose
		// eagerly to allow easier garbage collection of the renderer
		internal InnerGestureListener(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership)
		{
		}

		internal void OnTouchEvent(MotionEvent e)
		{
			// TODO hartez 2017/08/23 10:36:33 The FastRenderers GestureManager doesn't call this at all	
			// - maybe it's not really necessary?

			if (e.Action == MotionEventActions.Up)
				EndScrolling();
			else if (e.Action == MotionEventActions.Move)
				StartScrolling(e);
		}

		bool GestureDetector.IOnDoubleTapListener.OnDoubleTap(MotionEvent e)
		{
			if (_disposed)
				return false;

			return _tapDelegate(2);
		}

		bool GestureDetector.IOnDoubleTapListener.OnDoubleTapEvent(MotionEvent e)
		{
			return false;
		}

		bool GestureDetector.IOnGestureListener.OnDown(MotionEvent e)
		{
			Debug.WriteLine($">>>>> InnerGestureListener OnDown 78: {e.Action}");

			SetStartingPosition(e);

			// TODO hartez This is suuuuuper inefficent; there's got to be a better way
			// TODO hartez 2017/09/07 14:20:02 Turning this off for now, don't think we really need it	
			//if (_tapGestureRecognizers(1).Any() || _tapGestureRecognizers(2).Any())
			//{
			//	// Need to return true for OnDown or the GestureDetector will ignore pretty much everything else
			//	return true;
			//}

			//return false;
			return true;
		}

		bool GestureDetector.IOnGestureListener.OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
		{
			EndScrolling();
			return false;
		}

		void GestureDetector.IOnGestureListener.OnLongPress(MotionEvent e)
		{
			SetStartingPosition(e);
		}

		bool GestureDetector.IOnGestureListener.OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
		{
			if (e1 == null || e2 == null)
				return false;

			SetStartingPosition(e1);

			return StartScrolling(e2);
		}

		void GestureDetector.IOnGestureListener.OnShowPress(MotionEvent e)
		{
		}

		bool GestureDetector.IOnGestureListener.OnSingleTapUp(MotionEvent e)
		{
			Debug.WriteLine($">>>>> InnerGestureListener OnSingleTapUp 123: MESSAGE");
			if (_disposed)
				return false;

			if (HasDoubleTapHandler())
			{
				// Because we have a handler for double-tap, we need to wait for
				// OnSingleTapConfirmed (to verify it's really just a single tap) before running the delegate
				return false;
			}

			// A single tap has occurred and there's no handler for double tap to worry about,
			// so we can go ahead and run the delegate
			return _tapDelegate(1);
		}

		bool GestureDetector.IOnDoubleTapListener.OnSingleTapConfirmed(MotionEvent e)
		{
			Debug.WriteLine($">>>>> InnerGestureListener OnSingleTapConfirmed 141: MESSAGE");
			if (_disposed)
				return false;

			if (!HasDoubleTapHandler())
			{
				// We're not worried about double-tap, so OnSingleTap has already run the delegate
				// there's nothing for us to do here
				return false;
			}

			// Since there was a double-tap handler, we had to wait for OnSingleTapConfirmed;
			// Now that we're sure it's a single tap, we can run the delegate
			return _tapDelegate(1);
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (disposing)
			{
				_tapDelegate = null;
				_tapGestureRecognizers = null;
				_scrollDelegate = null;
				_scrollStartedDelegate = null;
				_scrollCompleteDelegate = null;
			}

			base.Dispose(disposing);
		}

		void SetStartingPosition(MotionEvent e1)
		{
			_lastX = e1.GetX();
			_lastY = e1.GetY();
		}

		bool StartScrolling(MotionEvent e2)
		{
			if (_scrollDelegate == null)
				return false;

			if (!_isScrolling && _scrollStartedDelegate != null)
				_scrollStartedDelegate(e2.PointerCount);

			_isScrolling = true;

			float totalX = e2.GetX() - _lastX;
			float totalY = e2.GetY() - _lastY;

			return _scrollDelegate(totalX, totalY, e2.PointerCount);
		}

		void EndScrolling()
		{
			if (_isScrolling && _scrollCompleteDelegate != null)
				_scrollCompleteDelegate();

			_isScrolling = false;
		}

		bool HasDoubleTapHandler()
		{
			if (_tapGestureRecognizers == null)
				return false;
			return _tapGestureRecognizers(2).Any();
		}
	}
}