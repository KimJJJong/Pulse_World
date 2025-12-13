/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    public static class SideSlot
    {
        public static int ToSlot(char side) => side switch
        {
            'A' => 0,
            'B' => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(side), $"Unknown side: {side}")
        };

        public static char ToSide(int slot) => slot switch
        {
            0 => 'A',
            1 => 'B',
            _ => throw new ArgumentOutOfRangeException(nameof(slot), $"Unknown slot: {slot}")
        };
    }

*/