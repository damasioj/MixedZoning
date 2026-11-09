using System;
using ColossalFramework;
using UnityEngine;

namespace MixedZoning.Zones
{
    /// <summary>
    /// Works out which zone cell a terrain overlay sample belongs to, as whole-number pattern
    /// coordinates laid out along the cell grid, so overlay patterns turn with the road.
    ///
    /// Each 4 m terrain sample stores the offset to its cell's centre, but rounded to 1/16 m, so
    /// samples of the same cell disagree slightly. To get one exact answer per cell, the owning
    /// ZoneBlock is looked up and the cell centre is recomputed from the block itself.
    /// Coordinates: a = steps across the road, b = steps along it.
    /// </summary>
    internal static class ZoneCellLocator
    {
        private const double HalfMap = 8640.0, SampleSize = 4.0, CellSize = 8.0, OffsetScale = 15.9375;

        public static void CellCoords(int x, int z, TerrainManager.ZoneCell cell, out int a, out int b)
        {
            // Approximate centre of the sample's cell, from the stored (rounded) offset.
            double cx = x * SampleSize - HalfMap + SampleSize / 2 + cell.m_offsetX / OffsetScale;
            double cz = z * SampleSize - HalfMap + SampleSize / 2 + cell.m_offsetZ / OffsetScale;
            var zone = (ItemClass.Zone)(cell.m_zone & 15);

            ZoneManager zm = Singleton<ZoneManager>.exists ? Singleton<ZoneManager>.instance : null;
            if (zm?.m_blocks?.m_buffer != null && zm.m_zoneGrid != null
                && TryExactCentre(zm.m_blocks.m_buffer, zm.m_zoneGrid, cx, cz, zone, out double ex, out double ez, out double angle))
            {
                Project(ex, ez, angle, out a, out b);
                return;
            }
            // No block found (shouldn't happen): use the stored angle (only known modulo 90°).
            Project(cx, cz, cell.m_angle * (Math.PI / 512.0), out a, out b);
        }

        /// <summary>
        /// Find the block cell whose centre is (approximately) (cx, cz) and holds this zone.
        /// Returns its exact centre and the block's angle.
        /// </summary>
        internal static bool TryExactCentre(ZoneBlock[] blocks, ushort[] grid, double cx, double cz, ItemClass.Zone zone,
                                            out double ex, out double ez, out double angle)
        {
            int gx = Mathf.Clamp((int)(cx / 64.0 + 75.0), 0, 149);
            int gz = Mathf.Clamp((int)(cz / 64.0 + 75.0), 0, 149);
            for (int iz = Math.Max(gz - 1, 0); iz <= Math.Min(gz + 1, 149); iz++)
            {
                for (int ix = Math.Max(gx - 1, 0); ix <= Math.Min(gx + 1, 149); ix++)
                {
                    int guard = 0;
                    for (ushort id = grid[iz * 150 + ix]; id != 0 && guard++ < 49152; id = blocks[id].m_nextGridBlock)
                    {
                        ZoneBlock blk = blocks[id];
                        if ((blk.m_flags & 3) != 1) continue;   // created and not deleted

                        double cos = Math.Cos(blk.m_angle), sin = Math.Sin(blk.m_angle);
                        double dx = cx - blk.m_position.x, dz = cz - blk.m_position.z;
                        // Block axes (ZoneBlock.ZonesUpdated): v = (cos, sin)*8 across, v2 = (sin, -cos)*8 along.
                        double k = (dx * cos + dz * sin) / CellSize + 3.5;
                        double i = (dx * sin - dz * cos) / CellSize + 3.5;
                        int ki = (int)Math.Round(k), ii = (int)Math.Round(i);
                        if (ki < 0 || ki > 3 || ii < 0 || ii >= blk.RowCount) continue;
                        if (Math.Abs(k - ki) > 0.25 || Math.Abs(i - ii) > 0.25) continue;
                        if (blk.GetZone(ki, ii) != zone) continue;

                        ex = blk.m_position.x + ((ki - 3.5) * cos + (ii - 3.5) * sin) * CellSize;
                        ez = blk.m_position.z + ((ki - 3.5) * sin - (ii - 3.5) * cos) * CellSize;
                        angle = blk.m_angle;
                        return true;
                    }
                }
            }
            ex = ez = angle = 0;
            return false;
        }

        /// <summary>
        /// Cell coordinates along the grid. The angle is taken modulo 180°, so blocks on both
        /// sides of a road share axes: a runs across the road, b along it.
        /// </summary>
        internal static void Project(double ex, double ez, double angle, out int a, out int b)
        {
            angle %= Math.PI;
            if (angle < 0) angle += Math.PI;
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            a = (int)Math.Floor((ex * cos + ez * sin) / CellSize + 0.5);
            b = (int)Math.Floor((ex * sin - ez * cos) / CellSize + 0.5);
        }
    }
}
