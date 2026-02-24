using System.Collections.Generic;

namespace SideBar_Nav.Models
{
    public class ReaderSettingsSnapshot
    {
        public uint SelectedRfMode { get; set; }
        public int SelectedSearchMode { get; set; }
        public ushort SelectedSession { get; set; }
        public int SelectedReportMode { get; set; }

        public bool IncludeLastSeenTime { get; set; }
        public bool IncludePeakRssi { get; set; }
        public bool IncludePhaseAngle { get; set; }
        public bool IncludeFirstSeenTime { get; set; }
        public bool IncludeSeenCount { get; set; }

        public bool IsAntenna1Enabled { get; set; }
        public bool IsAntenna2Enabled { get; set; }
        public bool IsAntenna3Enabled { get; set; }
        public bool IsAntenna4Enabled { get; set; }

        public int TxPower1 { get; set; }
        public int TxPower2 { get; set; }
        public int TxPower3 { get; set; }
        public int TxPower4 { get; set; }

        public int RxSensitivity1 { get; set; }
        public int RxSensitivity2 { get; set; }
        public int RxSensitivity3 { get; set; }
        public int RxSensitivity4 { get; set; }

        public Dictionary<ushort, string> AntennaNameMap { get; set; } = new();

        public bool IsEpcFilteringEnabled { get; set; }
        public bool IsUserMemoryFilteringEnabled { get; set; }
        public string? EpcFilterMask { get; set; }
        public ushort EpcBitPointer { get; set; }
        public int EpcBitCount { get; set; }
        public string? UserFilterMask { get; set; }
        public ushort UserBitPointer { get; set; }
        public int UserBitCount { get; set; }
        public bool IsAdvancedFilteringEnabled { get; set; }
        public List<string> HexFilterValues { get; set; } = new();

        public static ReaderSettingsSnapshot CreateDefault()
        {
            var snapshot = new ReaderSettingsSnapshot
            {
                SelectedRfMode = 2,
                SelectedSearchMode = 0,
                SelectedReportMode = 1,
                SelectedSession = 0,
                IncludeLastSeenTime = false,
                IncludePeakRssi = false,
                IncludePhaseAngle = false,
                IncludeFirstSeenTime = false,
                IncludeSeenCount = false,
                IsAntenna1Enabled = true,
                IsAntenna2Enabled = false,
                IsAntenna3Enabled = false,
                IsAntenna4Enabled = false,
                TxPower1 = 20,
                TxPower2 = 20,
                TxPower3 = 20,
                TxPower4 = 20,
                RxSensitivity1 = -55,
                RxSensitivity2 = -55,
                RxSensitivity3 = -55,
                RxSensitivity4 = -55,
                EpcBitPointer = 32,
                EpcBitCount = 16,
                UserBitPointer = 2,
                UserBitCount = 1
            };

            snapshot.AntennaNameMap[1] = "Anten 1";
            return snapshot;
        }
    }
}
