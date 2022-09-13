﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges.Events;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu
{
    public class OsuInputManager : RulesetInputManager<OsuAction>
    {
        private readonly OsuTouchInputMapper touchInputMapper;

        public IEnumerable<OsuAction> PressedActions => KeyBindingContainer.PressedActions;

        public bool AllowUserPresses
        {
            set => ((OsuKeyBindingContainer)KeyBindingContainer).AllowUserPresses = value;
        }

        /// <summary>
        /// Whether the user's cursor movement events should be accepted.
        /// Can be used to block only movement while still accepting button input.
        /// </summary>
        public bool AllowUserCursorMovement { get; set; } = true;

        protected override KeyBindingContainer<OsuAction> CreateKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
            => new OsuKeyBindingContainer(ruleset, variant, unique);

        public OsuInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
            touchInputMapper = new OsuTouchInputMapper(this) { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Add(touchInputMapper);
        }

        protected override bool Handle(UIEvent e)
        {
            if ((e is MouseMoveEvent || e is TouchMoveEvent) && !AllowUserCursorMovement) return false;

            return base.Handle(e);
        }

        protected override bool HandleMouseTouchStateChange(TouchStateChangeEvent e)
        {
            var source = e.Touch.Source;

            if (touchInputMapper.IsTapTouch(source))
                return true;

            return base.HandleMouseTouchStateChange(e);
        }

        /// <summary>
        /// Disables mouse action input for the first touch, so all the tapping must be done by other fingers.
        /// </summary>
        /// <returns>Whether we entered an touch tap only mapping state</returns>
        public bool HandleTouchTapOnlyMapping()
        {
            // We don't want to block the default cursor touch action input when the default cursor touch isn't a proper cursor touch.
            // this because it will completely block the first input with mods which don't accept cursor input such as autopilot.
            if (!touchInputMapper.IsCursorTouch(OsuTouchInputMapper.DEFAULT_CURSOR_TOUCH))
                return false;

            Vector2? cursorTouchPosition = CurrentState.Touch.GetTouchPosition(OsuTouchInputMapper.DEFAULT_CURSOR_TOUCH);

            // We shouldn't disable the cursor action input if the cursor isn't even active in the first place.
            if (cursorTouchPosition == null)
                return false;

            // Disables the actions for the cursor touch, all tapping must be done by the other fingers now.
            base.HandleMouseTouchStateChange(new TouchStateChangeEvent(CurrentState, null, new Touch(OsuTouchInputMapper.DEFAULT_CURSOR_TOUCH, cursorTouchPosition.Value), false, cursorTouchPosition));

            return true;
        }

        private class OsuKeyBindingContainer : RulesetKeyBindingContainer
        {
            public bool AllowUserPresses = true;

            public OsuKeyBindingContainer(RulesetInfo ruleset, int variant, SimultaneousBindingMode unique)
                : base(ruleset, variant, unique)
            {
            }

            protected override bool Handle(UIEvent e)
            {
                if (!AllowUserPresses) return false;

                return base.Handle(e);
            }
        }
    }

    public enum OsuAction
    {
        [Description("Left button")]
        LeftButton,

        [Description("Right button")]
        RightButton
    }
}
