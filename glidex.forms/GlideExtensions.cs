﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Bumptech.Glide;
using Java.Util.Concurrent;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using static Bumptech.Glide.Glide;

namespace Android.Glide
{
	public static class GlideExtensions
	{
		public static async Task LoadViaGlide (this ImageView imageView, ImageSource source, CancellationToken token)
		{
			try {
				if (!IsActivityAlive (imageView, source)) {
					CancelGlide (imageView);
					return;
				}

				RequestManager request = With (imageView.Context);
				RequestBuilder builder = null;

				if (source is null) {
					Forms.Debug ("`{0}` is null, clearing image", nameof (ImageSource));
					Clear (request, imageView);
					return;
				}

				switch (source) {
					case FileImageSource fileSource:
						var fileName = fileSource.File;
						var drawable = ResourceManager.GetDrawableByName (fileName);
						if (drawable != 0) {
							Forms.Debug ("Loading `{0}` as an Android resource", fileName);
							builder = request.Load (drawable);
						} else {
							Forms.Debug ("Loading `{0}` from disk", fileName);
							builder = request.Load (fileName);
						}
						break;

					case UriImageSource uriSource:
						var url = uriSource.Uri.OriginalString;
						Forms.Debug ("Loading `{0}` as a web URL", url);
						builder = request.Load (url);
						break;

					case StreamImageSource streamSource:
						Forms.Debug ("Loading `{0}` as a byte[]. Consider using `AndroidResource` instead, as it would be more performant", nameof (StreamImageSource));
						using (var memoryStream = new MemoryStream ())
						using (var stream = await streamSource.Stream (token)) {
							if (token.IsCancellationRequested || stream == null)
								return;
							if (!IsActivityAlive (imageView, source)) {
								CancelGlide (imageView);
								return;
							}
							await stream.CopyToAsync (memoryStream, token);
							builder = request.Load (memoryStream.ToArray ());
						}
						break;
				}

				var handler = Forms.GlideHandler;
				if (handler != null) {
					Forms.Debug ("Calling into {0} of type `{1}`.", nameof (IGlideHandler), handler.GetType ());
					if (handler.Build (imageView, source, builder, token)) {
						return;
					}
				}

				if (builder is null) {
					Clear (request, imageView);
				} else {
					imageView.Visibility = ViewStates.Visible;
					builder.Into (imageView);
				}
			} catch (Exception exc) {
				//Since developers can't catch this themselves, I think we should log it and silently fail
				Forms.Warn ("Unexpected exception in glidex: {0}", exc);
			}
		}

		public static async Task<Bitmap> LoadViaGlide (this ImageSource source, Context context, CancellationToken token)
		{
			try {
				if (!IsActivityAlive (context, source))
					return null;

				RequestManager request = With (context);
				RequestBuilder builder = null;

				if (source is null) {
					Forms.Debug ("`{0}` is null", nameof (ImageSource));
					return null;
				}

				switch (source) {
					case FileImageSource fileSource:
						var fileName = fileSource.File;
						var drawable = ResourceManager.GetDrawableByName (fileName);
						if (drawable != 0) {
							Forms.Debug ("Loading `{0}` as an Android resource", fileName);
							builder = request.AsBitmap ().Load (drawable);
						} else {
							Forms.Debug ("Loading `{0}` from disk", fileName);
							builder = request.AsBitmap ().Load (fileName);
						}
						break;

					case UriImageSource uriSource:
						var url = uriSource.Uri.OriginalString;
						Forms.Debug ("Loading `{0}` as a web URL", url);
						builder = request.AsBitmap ().Load (url);
						break;

					case StreamImageSource streamSource:
						Forms.Debug ("Loading `{0}` as a byte[]. Consider using `AndroidResource` instead, as it would be more performant", nameof (StreamImageSource));
						using (var memoryStream = new MemoryStream ())
						using (var stream = await streamSource.Stream (token)) {
							if (token.IsCancellationRequested || stream == null)
								return null;
							if (!IsActivityAlive (context, source)) {
								return null;
							}
							await stream.CopyToAsync (memoryStream, token);
							builder = request.AsBitmap ().Load (memoryStream.ToArray ());
						}
						break;
				}

				var handler = Forms.GlideHandler;
				if (handler != null) {
					Forms.Debug ("Calling into {0} of type `{1}`.", nameof (IGlideHandler), handler.GetType ());
					handler.Build (source, builder, token);
				}

				if (builder is null) {
					return null;
				} else {
					var result = await builder.Submit ().GetAsync ();
					return (Bitmap) result;
				}

			} catch (Exception exc) {
				//Since developers can't catch this themselves, I think we should log it and silently fail
				Forms.Warn ("Unexpected exception in glidex: {0}", exc);
				return null;
			}
		}

		static bool IsActivityAlive (Context context, ImageSource source)
		{
			if (context == null || context.Handle == IntPtr.Zero) {
				Forms.Warn ("imageView.Handle is IntPtr.Zero, aborting image load for `{0}`.", source);
				return false;
			}

			//NOTE: in some cases ContextThemeWrapper is Context
			var activity = context as Activity ?? Forms.Activity;
			if (activity != null) {
				if (activity.IsFinishing) {
					Forms.Warn ("Activity of type `{0}` is finishing, aborting image load for `{1}`.", activity.GetType ().FullName, source);
					return false;
				}
				if (activity.IsDestroyed) {
					Forms.Warn ("Activity of type `{0}` is destroyed, aborting image load for `{1}`.", activity.GetType ().FullName, source);
					return false;
				}
			} else {
				Forms.Warn ("Context `{0}` is not an Android.App.Activity and could not use Android.Glide.Forms.Activity, aborting image load for `{1}`.", context, source);
				return false;
			}

			return true;
		}

		/// <summary>
		/// NOTE: see https://github.com/bumptech/glide/issues/1484#issuecomment-365625087
		/// </summary>
		static bool IsActivityAlive (ImageView imageView, ImageSource source)
		{
			// The imageView.Handle could be IntPtr.Zero? Meaning we somehow have a reference to a disposed ImageView...
			// I think this is within the realm of "possible" after the await call in LoadViaGlide().
			if (imageView.Handle == IntPtr.Zero) {
				Forms.Warn ("imageView.Handle is IntPtr.Zero, aborting image load for `{0}`.", source);
				return false;
			}

			//NOTE: in some cases ContextThemeWrapper is Context
			var activity = imageView.Context as Activity ?? Forms.Activity;
			if (activity != null) {
				if (activity.IsFinishing) {
					Forms.Warn ("Activity of type `{0}` is finishing, aborting image load for `{1}`.", activity.GetType ().FullName, source);
					return false;
				}
				if (activity.IsDestroyed) {
					Forms.Warn ("Activity of type `{0}` is destroyed, aborting image load for `{1}`.", activity.GetType ().FullName, source);
					return false;
				}
			} else {
				Forms.Warn ("Context `{0}` is not an Android.App.Activity and could not use Android.Glide.Forms.Activity, aborting image load for `{1}`.", imageView.Context, source);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Cancels the Request and "clears" the ImageView
		/// </summary>
		static void Clear (RequestManager request, ImageView imageView)
		{
			imageView.Visibility = ViewStates.Gone;
			imageView.SetImageBitmap (null);

			//We need to call Clear for Glide to know this image is now unused
			//https://bumptech.github.io/glide/doc/targets.html
			request.Clear (imageView);
		}

		internal static void CancelGlide (this ImageView imageView)
		{
			if (imageView.Handle == IntPtr.Zero) {
				return;
			}

			//NOTE: we may be doing a Cancel after the Activity has just exited
			// To make this work we have to use the Application.Context
			With (App.Application.Context).Clear (imageView);
		}
	}
}
