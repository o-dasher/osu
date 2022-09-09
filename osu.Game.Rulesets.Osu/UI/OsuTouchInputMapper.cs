﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace osu.Game.Rulesets.Osu.UI
{
    public class OsuTouchInputMapper : Drawable
    {
        public const TouchSource CURSOR_TOUCH = TouchSource.Touch1;

        /// <summary>
        /// How many taps (taps referring as streaming touch input) can be registered.
        /// </summary>
        private const int tap_touches_limit = 2;

        /// <summary>
        /// How many concurrent touches can be registered.
        /// </summary>
        private const int concurrent_touches_limit = tap_touches_limit + 1;

        /// <summary>
        /// The index for the last concurrent able touch.
        /// </summary>
        private const int last_concurrent_touch_index = concurrent_touches_limit - 1;

        private readonly HashSet<TouchSource> allowedTouchSources = Enum.GetValues(typeof(TouchSource)).Cast<TouchSource>().Take(concurrent_touches_limit).ToHashSet();

        private readonly Dictionary<TouchSource, OsuAction> touchActions = new Dictionary<TouchSource, OsuAction>();

        private readonly OsuInputManager osuInputManager;

        private int getTouchIndex(TouchSource source) => source - TouchSource.Touch1;

        public OsuTouchInputMapper(OsuInputManager inputManager)
        {
            osuInputManager = inputManager;
            foreach (var source in allowedTouchSources)
                touchActions.Add(source, getTouchIndex(source) % 2 == 0 ? OsuAction.LeftButton : OsuAction.RightButton);
        }

        private bool isTapTouch(TouchSource source) => source != CURSOR_TOUCH || !osuInputManager.AllowUserCursorMovement;

        private bool isCursorTouch(TouchSource source) => !isTapTouch(source);

        private bool isValidTouchInput(int index) => index <= last_concurrent_touch_index;

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            var source = e.Touch.Source;
            int sourceIndex = getTouchIndex(source);

            if (!isValidTouchInput(sourceIndex))
                return false;

            osuInputManager.DragMode = sourceIndex == last_concurrent_touch_index;

            if (isCursorTouch(source))
                return base.OnTouchDown(e);

            osuInputManager.KeyBindingContainer.TriggerPressed(touchActions[source]);

            return true;
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            var source = e.Touch.Source;
            int sourceIndex = getTouchIndex(source);

            if (!isValidTouchInput(sourceIndex))
                return;

            if (isTapTouch(source))
                osuInputManager.KeyBindingContainer.TriggerReleased(touchActions[source]);

            base.OnTouchUp(e);
        }
    }
}
