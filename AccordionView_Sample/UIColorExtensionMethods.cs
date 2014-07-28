using System;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using System.Collections.Generic;

namespace AccordionView_Sample
{
	public static class UIColorExtensionMethods
	{
		public static UIColor ToColor (this string color)
		{
			var type = typeof(UIColor);
			var colorProp = type.GetProperty(color);
			var uiColor = (UIColor)colorProp.GetGetMethod().Invoke(null, new object[0]);
			return uiColor;
		}
	}
}
