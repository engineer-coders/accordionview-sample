using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace AccordionView_Sample
{
	[Register("AccordionView")]
	public class AccordionView : UIView
	{
		private class Entry
		{
			public UIView View { get; set; }
			public UIButton Header { get; set; }
			public SizeF OriginalSize { get; set; }
		}

		readonly List<Entry> _entries = new List<Entry>();			
		UIScrollView _scrollView;

		private int? _entryIndexJustOpened = null;

		private List<int> _selectionIndicies;
		public List<int> SelectionIndicies { 
			get {
				return _selectionIndicies;
			}
			set {
				DoSelectIndexes (value);
			}
		}

		public enum Mode
		{
			SingleSelection,
			SingleSelection_OneAlwaysOpen,
			MultipleSelection
		}

		TimeSpan AnimationDuration { get; set; }
		public UIViewAnimationOptions AnimationOptions { get; set; }
		public Mode DisplayMode { get; set; }
		public float HeaderButtonHeight {get;set;}
		public UIColor DefaultButtonBackgroundColor { get; set; }
		public UIColor DefaultButtonTextColor { get; set; }

		public event EventHandler SelectionChanged; 

		public AccordionView ()
		{
			Initialise();
		}

		public AccordionView (IntPtr handle)
			: base(handle)
		{
			Initialise();
		}

		[Export("initWithFrame:")]
		public AccordionView (RectangleF frame)
			: base(frame)
		{
			Initialise();
		}

		private void Initialise()
		{
			_selectionIndicies = new List<int>();
			AnimationDuration = TimeSpan.FromSeconds(0.3);
			AnimationOptions = UIViewAnimationOptions.CurveEaseIn;
			DisplayMode = Mode.SingleSelection_OneAlwaysOpen;
			HeaderButtonHeight = 45f;

			BackgroundColor = UIColor.Clear;
			AutosizesSubviews = false;
			DefaultButtonBackgroundColor = UIColor.Black;
			DefaultButtonTextColor = UIColor.White;

			_scrollView = new UIScrollView(Frame)
			{
				BackgroundColor = UIColor.Clear,
				UserInteractionEnabled = true,
				ScrollsToTop = false,
				AutosizesSubviews = false
			};
			AddSubview(_scrollView);

			_scrollView.Scrolled += HandleScrollViewScrolled;
		}

		public void Add (string text, UIView view)
		{
			UIButton header = new UIButton(new RectangleF(0, 0, 320, HeaderButtonHeight));
			header.SetTitle(text, UIControlState.Normal);
			header.TitleLabel.TextAlignment = UITextAlignment.Left;
			header.BackgroundColor = DefaultButtonBackgroundColor;
			header.SetTitleColor(DefaultButtonTextColor, UIControlState.Normal);
			Add(header, view);
		}

		public void Add (UIButton header, UIView view)
		{
			if (header == null)
				throw new ArgumentNullException ("header");

			if (view == null)
				throw new ArgumentNullException ("view");

			_entries.Add (new Entry ()
				{
					Header = header,
					View = view,
					OriginalSize = view.Frame.Size
				});

			view.ClipsToBounds = true;
			view.AutoresizingMask = UIViewAutoresizing.None;

			var headerFrame = header.Frame;

			headerFrame.X = 0;
			headerFrame.Width = Frame.Width;
			header.Frame = headerFrame;

			var viewFrame = view.Frame;
			viewFrame.X = 0;
			viewFrame.Width = Frame.Width;
			viewFrame.Height = 0;
			view.Frame = viewFrame;

			_scrollView.AddSubview (view);
			_scrollView.AddSubview (header);

			var insertionPosition = _entries.Count - 1;
			header.TouchUpInside += (sender, args) => OnHeaderTouchUpInside (insertionPosition);
		}

		void OnHeaderTouchUpInside (int headerIndex)
		{
			var newList = new List<int> ();
			newList.AddRange (SelectionIndicies);
			bool makeVisible = !newList.Contains (headerIndex);

			if (makeVisible) {
				if (DisplayMode != Mode.MultipleSelection) {
					newList.Clear ();
				}
				newList.Add (headerIndex);
				_entryIndexJustOpened = headerIndex;
			} else {
				if (DisplayMode == Mode.SingleSelection_OneAlwaysOpen)
				{
					return;
				}
				newList.Remove (headerIndex);
				_entryIndexJustOpened = null;
			}
			SelectionIndicies = newList;
		}

		private void DoSelectIndexes (List<int> toSelect)
		{
			if (toSelect == null) {
				toSelect = new List<int>();
			}

			var listToSelect = toSelect.ToList ();
			if (DisplayMode != Mode.MultipleSelection  && listToSelect.Count > 1) {
				throw new ArgumentOutOfRangeException ("toSelect", "multiselection not enabled");
			}

			if (DisplayMode == Mode.SingleSelection_OneAlwaysOpen  && listToSelect.Count != 1) {
				throw new ArgumentOutOfRangeException ("toSelect", "for SingleSelection_OneAlwaysOpen you must always select one item");
			}

			if (listToSelect.Any (i => i < 0 || i >= _entries.Count)) {
				throw new ArgumentOutOfRangeException ("toSelect", "one or more indexes out of range");
			}

			_selectionIndicies = listToSelect;
			SetNeedsLayout ();

			var handler = this.SelectionChanged;
			if (handler != null) {
				handler(this, EventArgs.Empty);
			}
		}

		public override void LayoutSubviews ()
		{
			if (_scrollView == null) {
				return;
			}

			_scrollView.Frame = Frame;

			var contentHeight = LayoutEntriesAndCalculateHeight ();

			// Make sure that the yScrollOffset shrinks if needed.
			var yScrollOffset = _scrollView.ContentOffset.Y;
			if (yScrollOffset + _scrollView.Frame.Height >= contentHeight) {
				yScrollOffset = contentHeight - _scrollView.Frame.Height;
			}

			// Make sure that the yScrollOffset grows if needed.
			if (_entryIndexJustOpened.HasValue) {
				var lastTouchedEntry = _entries [_entryIndexJustOpened.Value];
				var bottomOfEntry = lastTouchedEntry.Header.Frame.Bottom + lastTouchedEntry.OriginalSize.Height;
				var neededScrollYToSeeWholeEntry = bottomOfEntry - _scrollView.Frame.Height;
				if (neededScrollYToSeeWholeEntry > yScrollOffset)
				{
					yScrollOffset = neededScrollYToSeeWholeEntry;
				}
				_entryIndexJustOpened = null;
			}

			// Just in case any changes above have broken things.
			if (yScrollOffset < 0) {
				yScrollOffset = 0;
			}

			UIView.Animate(AnimationDuration.TotalSeconds,
				0,
				AnimationOptions,
				() => {
					_scrollView.ContentSize = new SizeF (Frame.Width, contentHeight);
					var rectangle = _scrollView.Frame;
					rectangle.Y = yScrollOffset;
					_scrollView.ScrollRectToVisible(rectangle, false);
				},
				() => {
				});

			_scrollView.SetContentOffset(new PointF(0, yScrollOffset), true);
			HandleScrollViewChange();
			base.LayoutSubviews ();
		}

		void HandleScrollViewScrolled (object sender, EventArgs e)
		{
			HandleScrollViewChange();
		}

		private float LayoutEntriesAndCalculateHeight ()
		{
			float contentHeightSoFar = 0;

			for (int i=0; i<_entries.Count; i++) {
				var entry = _entries[i];

				var viewFrame = entry.View.Frame;
				var headerFrame = entry.Header.Frame;

				headerFrame.Y = contentHeightSoFar;
				contentHeightSoFar += headerFrame.Height;
				viewFrame.Y = contentHeightSoFar;

				bool hideViewAfterAnimation = false;
				if (SelectionIndicies.Contains(i))
				{
					viewFrame.Height = entry.OriginalSize.Height;
					entry.View.Hidden = false;
				}
				else
				{
					hideViewAfterAnimation = true;
					viewFrame.Height = 0;
				}

				contentHeightSoFar += viewFrame.Height;

				if (!entry.View.Frame.Equals(viewFrame))
				{
					UIView.Animate(AnimationDuration.TotalSeconds, 0, AnimationOptions,
						() => {
							entry.View.Frame = viewFrame;
							entry.View.Bounds = new RectangleF(0,0,viewFrame.Width,viewFrame.Height);
							entry.Header.Frame = headerFrame;
						},
						() => {
							if (hideViewAfterAnimation)
							{
								entry.View.Hidden = true;
							}
						});
				}
			}

			return contentHeightSoFar;
		}

		void HandleScrollViewChange ()
		{
			for (var i=0; i<_entries.Count; i++)
			{
				var entry = _entries[i];
				if (entry.View.Frame.Height > 0)
				{
					var frameForHeaderAndContent = entry.View.Frame;
					frameForHeaderAndContent.Y = frameForHeaderAndContent.Y - entry.Header.Frame.Height;
					frameForHeaderAndContent.Height = frameForHeaderAndContent.Height + entry.Header.Frame.Height;

					var scrollOffset = _scrollView.ContentOffset;
					var frameForHeader = entry.Header.Frame;

					// If current view is on screen.
					if (frameForHeaderAndContent.Contains(scrollOffset))
					{
						var normalHeaderYLocation = frameForHeaderAndContent.Y - scrollOffset.Y;

						if (normalHeaderYLocation < frameForHeader.Height)
						{
							frameForHeader.Y = scrollOffset.Y;
						}
						else
						{
							frameForHeader.Y = normalHeaderYLocation;
						}
					}
					else
					{
						frameForHeader.Y = frameForHeaderAndContent.Y;
					}

					entry.Header.Frame = frameForHeader;
				}
			}	
		}
	}
}
	