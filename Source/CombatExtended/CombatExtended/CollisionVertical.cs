﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public struct CollisionVertical
    {
        private const float TreeCollisionHeight = 5f;        // Trees are this tall
        private const float WallCollisionHeight = 2f;       // Walls are this tall
        public const float BodyRegionBottomHeight = 0.45f;  // Hits below this percentage will impact the corresponding body region
        public const float BodyRegionMiddleHeight = 0.85f;  // This also sets the altitude at which pawns hold their guns

        private readonly FloatRange heightRange;
        public readonly float shotHeight;

        public FloatRange HeightRange => new FloatRange(heightRange.min, heightRange.max);
        public float Min => heightRange.min;
        public float Max => heightRange.max;
        public float BottomHeight => Max * BodyRegionBottomHeight;
        public float MiddleHeight => Max * BodyRegionMiddleHeight;

        public CollisionVertical(Thing thing)
        {
            CalculateHeightRange(thing, out heightRange, out shotHeight);
        }

        private static void CalculateHeightRange(Thing thing, out FloatRange heightRange, out float shotHeight)
        {
            shotHeight = 0;
            if (thing == null)
            {
                heightRange = new FloatRange(0, 0);
                return;
            }
            if (thing is Building)
            {
                if (thing.def.Fillage == FillCategory.Full)
                {
                    heightRange = new FloatRange(0, WallCollisionHeight);
                    return;
                }
                if (thing.IsTree())
                {
                    heightRange = new FloatRange(0, TreeCollisionHeight);    // Check for trees
                    return;
                }
                float fillPercent = thing.def.fillPercent;
                heightRange = new FloatRange(Mathf.Min(0f, fillPercent), Mathf.Max(0f, fillPercent));
                shotHeight = fillPercent;
                return;
            }
            float collisionHeight = 0f;
            float shotHeightOffset = 0;
            var pawn = thing as Pawn;
            if (pawn != null)
            {
                collisionHeight = pawn.BodySize * CE_Utility.GetCollisionBodyFactors(pawn).Second;
                shotHeightOffset = collisionHeight * (1 - BodyRegionMiddleHeight);

                // Humanlikes in combat crouch to reduce their profile
                if (pawn.IsCrouching())
                {
                    float crouchHeight = BodyRegionBottomHeight * collisionHeight;  // Minimum height we can crouch down to

                    // Find the highest adjacent cover
                    Map map = pawn.Map;
                    foreach(IntVec3 curCell in GenAdjFast.AdjacentCells8Way(pawn.Position))
                    {
                        if (curCell.InBounds(map))
                        {
                            Thing cover = curCell.GetCover(map);
                            if (cover != null && cover.def.Fillage == FillCategory.Partial && !cover.IsTree())
                            {
                                var coverHeight = new CollisionVertical(cover).Max;
                                if (coverHeight > crouchHeight) crouchHeight = coverHeight;
                            }
                        }
                    }
                    collisionHeight = Mathf.Min(collisionHeight, crouchHeight + 0.01f + shotHeightOffset);  // We crouch down only so far that we can still shoot over our own cover and never beyond our own body size
                }
            }
            else
            {
                collisionHeight = thing.def.fillPercent;
            }
            var edificeHeight = 0f;
            if (thing.Map != null)
            {
                var edifice = thing.Position.GetCover(thing.Map);
                if (edifice != null && edifice.GetHashCode() != thing.GetHashCode() && !edifice.IsTree())
                {
                    edificeHeight = new CollisionVertical(edifice).heightRange.max;
                }
            }
            float fillPercent2 = collisionHeight;
            heightRange = new FloatRange(Mathf.Min(edificeHeight, edificeHeight + fillPercent2), Mathf.Max(edificeHeight, edificeHeight + fillPercent2));
            shotHeight = heightRange.max - shotHeightOffset;
        }

        /// <summary>
        /// Calculates the BodyPartHeight based on how high a projectile impacted in relation to overall pawn height.
        /// </summary>
        /// <param name="projectileHeight">The height of the projectile at time of impact.</param>
        /// <returns>BodyPartHeight between Bottom and Top.</returns>
        public BodyPartHeight GetCollisionBodyHeight(float projectileHeight)
        {
            if (projectileHeight < BottomHeight) return BodyPartHeight.Bottom;
            else if (projectileHeight < MiddleHeight) return BodyPartHeight.Middle;
            return BodyPartHeight.Top;
        }

        public BodyPartHeight GetRandWeightedBodyHeightBelow(float threshold)
        {
            return GetCollisionBodyHeight(Rand.Range(Min, threshold));
        }
    }
}