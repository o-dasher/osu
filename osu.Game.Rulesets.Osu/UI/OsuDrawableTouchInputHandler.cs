// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public class OsuDrawableTouchInputHandler : Drawable
    {
        public const TouchSource DEFAULT_CURSOR_TOUCH = TouchSource.Touch1;

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

        public readonly HashSet<TouchSource> AllowedTouchSources = Enum.GetValues(typeof(TouchSource)).Cast<TouchSource>().Take(concurrent_touches_limit).ToHashSet();

        private readonly Dictionary<TouchSource, OsuAction> touchActions = new Dictionary<TouchSource, OsuAction>();

        private readonly HashSet<TouchSource> activeAllowedTouchSources = new HashSet<TouchSource>();

        private readonly Playfield playfield;

        private readonly OsuInputManager osuInputManager;

        private int getTouchIndex(TouchSource source) => source - TouchSource.Touch1;

        private TouchSource cursorTouch = DEFAULT_CURSOR_TOUCH;

        private void updateTouchInformation()
        {
            var indexedTaps = AllowedTouchSources.Where(s => isTapTouch(s)).Select((source, index) => new { source, index }).ToDictionary(entry => entry.source, entry => entry.index);

            touchActions.Clear();

            foreach (var source in indexedTaps)
                touchActions.Add(source, indexedTaps[source] % 2 == 0 ? OsuAction.RightButton : OsuAction.LeftButton);
        }

        public OsuDrawableTouchInputHandler(DrawableOsuRuleset drawableRuleset)
        {
            playfield = drawableRuleset.Playfield;
            osuInputManager = (OsuInputManager)drawableRuleset.KeyBindingInputManager;

            updateTouchInformation();
        }

        private bool isTapTouch(TouchSource source) => source != DEFAULT_CURSOR_TOUCH || !osuInputManager.AllowUserCursorMovement;

        private bool isCursorTouch(TouchSource source) => !isTapTouch(source);

        private bool isValidTouchInput(int index) => index <= last_concurrent_touch_index;

        protected override void OnTouchMove(TouchMoveEvent e)
        {
            var aliveObjects = playfield.HitObjectContainer.AliveObjects;

            if (!aliveObjects.Any())
                return;

            var hitObject = aliveObjects.First();
            var hitObjectPosition = playfield.ToScreenSpace(hitObject.Position);

            var closestTouches = activeAllowedTouchSources.Select(source =>
            {
                var position = osuInputManager.CurrentState.Touch.GetTouchPosition(source);
                return new { source, position };
            }).Where(entry => entry.position != null).OrderByDescending(entry => Vector2.Distance((Vector2)entry.position!, hitObjectPosition));

            if (!closestTouches.Any())
                return;

            var closestTouch = closestTouches.First().source;

            if (closestTouch != cursorTouch)
            {
                cursorTouch = closestTouch;
                updateTouchInformation();
            }

            base.OnTouchMove(e);
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            var source = e.Touch.Source;
            int sourceIndex = getTouchIndex(source);

            if (!isValidTouchInput(sourceIndex))
                return false;

            osuInputManager.DragMode = sourceIndex == last_concurrent_touch_index;

            if (isCursorTouch(source))
                return base.OnTouchDown(e);

            activeAllowedTouchSources.Add(source);
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

            activeAllowedTouchSources.Remove(source);

            base.OnTouchUp(e);
        }
    }
}
