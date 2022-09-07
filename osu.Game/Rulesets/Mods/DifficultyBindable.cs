// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Mods
{
    public class DifficultyBindable : Bindable<float?>
    {
        public readonly IBindable<WorkingBeatmap> Beatmap = new Bindable<WorkingBeatmap>();

        /// <summary>
        /// Whether the extended limits should be applied to this bindable.
        /// </summary>
        public readonly BindableBool ExtendedLimits = new BindableBool();

        /// <summary>
        /// An internal numeric bindable to hold and propagate min/max/precision.
        /// The value of this bindable should not be set.
        /// </summary>
        internal readonly BindableFloat DisplayNumber = new BindableFloat
        {
            MinValue = 0,
            MaxValue = 10,
        };

        public float AppliedDifficulty;

        /// <summary>
        /// A function that can extract the current value of this setting from a beatmap difficulty for display purposes.
        /// </summary>
        public Func<IBeatmapDifficultyInfo, float>? ReadCurrentFromDifficulty;

        public readonly BindableBool RelativeDifficulty = new BindableBool();

        public float Precision
        {
            set => DisplayNumber.Precision = value;
        }

        private float minValue;

        public float MinValue
        {
            set
            {
                if (value == minValue)
                    return;

                minValue = value;
                updateMinValue();
            }
        }

        private float maxValue;

        public float MaxValue
        {
            set
            {
                if (value == maxValue)
                    return;

                maxValue = value;
                updateMaxValue();
            }
        }

        private float? extendedMaxValue;

        /// <summary>
        /// The maximum value to be used when extended limits are applied.
        /// </summary>
        public float? ExtendedMaxValue
        {
            set
            {
                if (value == extendedMaxValue)
                    return;

                extendedMaxValue = value;
                updateMaxValue();
            }
        }

        public DifficultyBindable()
            : this(null)
        {
        }

        private void updateMinMaxValues()
        {
            updateMinValue();
            updateMaxValue();
        }

        public DifficultyBindable(float? defaultValue = null)
            : base(defaultValue)
        {
            ExtendedLimits.BindValueChanged(_ => updateMinMaxValues());
            RelativeDifficulty.BindValueChanged(_ => updateMinMaxValues());
            Beatmap.BindValueChanged(_ =>
            {
                if (RelativeDifficulty.Value)
                    updateMinMaxValues();
            });
        }

        private float currentBeatmapDifficulty
        {
            get
            {
                Debug.Assert(ReadCurrentFromDifficulty != null);
                return ReadCurrentFromDifficulty(Beatmap.Value.Beatmap.BeatmapInfo.Difficulty);
            }
        }

        private bool isRelative => ReadCurrentFromDifficulty != null && RelativeDifficulty.Value && Beatmap.Value != null;

        public override float? Value
        {
            get => base.Value;
            set
            {
                if (value != null)
                {
                    // Ensure that in the case serialisation runs in the wrong order (and limit extensions aren't applied yet) the deserialised value is still propagated.
                    DisplayNumber.MaxValue = MathF.Max(DisplayNumber.MaxValue, value.Value);

                    AppliedDifficulty = isRelative ? currentBeatmapDifficulty + value.Value : value.Value;
                }

                base.Value = value;
            }
        }

        private float getAppliedMaxValue() => ExtendedLimits.Value && extendedMaxValue != null ? extendedMaxValue.Value : maxValue;

        private float getRelativeMaxValue(float appliedMaxValue) => appliedMaxValue - currentBeatmapDifficulty;

        private void updateMinValue()
        {
            if (isRelative)
            {
                float appliedMaxValue = getAppliedMaxValue();
                DisplayNumber.MinValue = getRelativeMaxValue(appliedMaxValue) - appliedMaxValue;
            }
            else
            {
                DisplayNumber.MinValue = minValue;
            }
        }

        private void updateMaxValue()
        {
            float appliedMaxValue = getAppliedMaxValue();
            DisplayNumber.MaxValue = isRelative ? getRelativeMaxValue(appliedMaxValue) : appliedMaxValue;
        }

        public override void BindTo(Bindable<float?> them)
        {
            if (!(them is DifficultyBindable otherDifficultyBindable))
                throw new InvalidOperationException($"Cannot bind to a non-{nameof(DifficultyBindable)}.");

            ReadCurrentFromDifficulty = otherDifficultyBindable.ReadCurrentFromDifficulty;

            // the following max value copies are only safe as long as these values are effectively constants.
            MaxValue = otherDifficultyBindable.maxValue;
            ExtendedMaxValue = otherDifficultyBindable.extendedMaxValue;

            ExtendedLimits.BindTarget = otherDifficultyBindable.ExtendedLimits;
            RelativeDifficulty.BindTarget = otherDifficultyBindable.RelativeDifficulty;

            Beatmap.BindTarget = otherDifficultyBindable.Beatmap;

            // the actual values need to be copied after the max value constraints.
            DisplayNumber.BindTarget = otherDifficultyBindable.DisplayNumber;
            base.BindTo(them);
        }

        public override void UnbindFrom(IUnbindable them)
        {
            if (!(them is DifficultyBindable otherDifficultyBindable))
                throw new InvalidOperationException($"Cannot unbind from a non-{nameof(DifficultyBindable)}.");

            base.UnbindFrom(them);

            DisplayNumber.UnbindFrom(otherDifficultyBindable.DisplayNumber);
            ExtendedLimits.UnbindFrom(otherDifficultyBindable.ExtendedLimits);
            RelativeDifficulty.UnbindFrom(otherDifficultyBindable.RelativeDifficulty);
            Beatmap.UnbindFrom(otherDifficultyBindable.Beatmap);
        }

        protected override Bindable<float?> CreateInstance() => new DifficultyBindable();
    }
}
