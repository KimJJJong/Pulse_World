using System;
using System.Collections.Generic;
using System.Linq;

namespace ApiServer.Domain.WaitingRoom;

public sealed class WaitingRoomHostSelectionCandidateDto
{
    public string Uid { get; set; } = "";
    public bool IsEligible { get; set; }
    public float CandidateCost { get; set; } = float.MaxValue;
    public float AveragePairCost { get; set; } = float.MaxValue;
    public float WorstPairCost { get; set; } = float.MaxValue;
    public int AveragePairRttMs { get; set; } = -1;
    public int WorstPairRttMs { get; set; } = -1;
    public int SteamPairCount { get; set; }
    public int MeasuredSteamPairCount { get; set; }
    public int ProxySteamPairCount { get; set; }
    public int ServerRelayPairCount { get; set; }
    public int UnavailablePairCount { get; set; }
    public float HostCapacityPenalty { get; set; }
    public bool SteamReady { get; set; }
    public int CurrentServerRttMs { get; set; } = -1;
    public float CurrentServerLossPct { get; set; }
    public int CurrentServerJitterMs { get; set; } = -1;
    public float AvgFrameMs { get; set; } = -1f;
    public float P95FrameMs { get; set; } = -1f;
    public bool UsesPartialProxyMetrics { get; set; }
    public List<string> DisqualifiedReasons { get; set; } = new();
}

public sealed class WaitingRoomHostSelectionSnapshotDto
{
    public string MetricVersion { get; set; } = WaitingRoomHostSelectionV1Calculator.MetricVersion;
    public string Mode { get; set; } = "EmergencyFallback";
    public string PreferredHostUid { get; set; } = "";
    public float PreferredHostScore { get; set; } = -1f;
    public int Epoch { get; set; }
    public long UpdatedAtMs { get; set; }
    public List<string> HostCandidateOrder { get; set; } = new();
    public List<WaitingRoomHostSelectionCandidateDto> Candidates { get; set; } = new();
}

public static class WaitingRoomHostSelectionV1Calculator
{
    public const string MetricVersion = "host-selection-v2-hybrid";

    private const long ReportFreshnessWindowMs = 30_000;
    private const int RelayProcessingAllowanceMs = 8;
    private const int DefaultSteamPairRttMs = 60;
    private const int DefaultSteamPairJitterMs = 10;
    private const float DefaultLossPct = 0.5f;
    private const float TieBreakEpsilon = 0.03f;

    public static WaitingRoomHostSelectionSnapshotDto Calculate(WaitingRoomDto room, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(room);

        var allMembers = (room.MemberUids ?? new List<string>())
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var participatingMembers = allMembers
            .Where(uid => string.Equals(uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase)
                          || !room.MemberReady.TryGetValue(uid, out var ready)
                          || ready)
            .ToList();

        if (participatingMembers.Count == 0)
            participatingMembers = allMembers.ToList();

        var stateByUid = (room.MemberTransport ?? new List<WaitingRoomMemberTransportDto>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Uid))
            .GroupBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(x => x.HostSelectionReportedAtMs)
                    .ThenByDescending(x => x.HostProbeReportedAtMs)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var candidates = new List<WaitingRoomHostSelectionCandidateDto>(allMembers.Count);
        foreach (var candidateUid in allMembers)
            candidates.Add(EvaluateCandidate(room, candidateUid, participatingMembers, stateByUid, nowMs));

        ApplyV2EligibilityAdjustments(room, candidates, participatingMembers.Count);

        int expectedPeerCount = Math.Max(0, participatingMembers.Count - 1);

        var sortedCandidates = candidates
            .OrderBy(x => x.IsEligible ? 0 : 1)
            .ThenBy(x => x.CandidateCost)
            .ThenBy(x => ComputePairRatio(x.ServerRelayPairCount, expectedPeerCount))
            .ThenByDescending(x => x.MeasuredSteamPairCount)
            .ThenBy(x => x.WorstPairCost)
            .ThenBy(x => string.Equals(x.Uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Uid, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string preferredUid = sortedCandidates.FirstOrDefault(x => x.IsEligible)?.Uid ?? "";
        if (string.IsNullOrWhiteSpace(preferredUid))
            preferredUid = ResolveEmergencyFallbackUid(room, stateByUid, allMembers);

        var orderedUids = BuildOrderedUids(preferredUid, sortedCandidates, room, stateByUid, allMembers);
        var preferredCandidate = sortedCandidates.FirstOrDefault(x => string.Equals(x.Uid, preferredUid, StringComparison.OrdinalIgnoreCase));

        string mode = ResolveMode(sortedCandidates, preferredCandidate, room);
        float preferredScore = preferredCandidate?.CandidateCost ?? -1f;

        return new WaitingRoomHostSelectionSnapshotDto
        {
            MetricVersion = MetricVersion,
            Mode = mode,
            PreferredHostUid = preferredUid,
            PreferredHostScore = preferredScore,
            Epoch = Math.Max(0, room.HostSelectionEpoch),
            UpdatedAtMs = Math.Max(nowMs, sortedCandidates.Max(x => x.CurrentServerRttMs >= 0 ? 1 : 0) > 0
                ? (stateByUid.Values.Max(GetLatestTransportReportedAtMs))
                : nowMs),
            HostCandidateOrder = orderedUids,
            Candidates = sortedCandidates
        };
    }

    private static WaitingRoomHostSelectionCandidateDto EvaluateCandidate(
        WaitingRoomDto room,
        string candidateUid,
        IReadOnlyList<string> participatingMembers,
        IReadOnlyDictionary<string, WaitingRoomMemberTransportDto> stateByUid,
        long nowMs)
    {
        stateByUid.TryGetValue(candidateUid, out var candidateState);
        var summary = new WaitingRoomHostSelectionCandidateDto
        {
            Uid = candidateUid,
            SteamReady = IsSteamUsable(candidateState),
            CurrentServerRttMs = candidateState?.CurrentServerRttMs ?? -1,
            CurrentServerLossPct = candidateState?.CurrentServerLossPct ?? 0f,
            CurrentServerJitterMs = candidateState?.CurrentServerJitterMs ?? -1,
            AvgFrameMs = candidateState?.AvgFrameMs ?? -1f,
            P95FrameMs = candidateState?.P95FrameMs ?? -1f
        };

        if (!participatingMembers.Contains(candidateUid, StringComparer.OrdinalIgnoreCase))
            summary.DisqualifiedReasons.Add("NotReady");

        if (candidateState == null)
        {
            summary.DisqualifiedReasons.Add("MissingState");
            return FinalizeCandidate(summary, pairCosts: Array.Empty<float>(), pairRtts: Array.Empty<int>(), expectedPeerCount: participatingMembers.Count - 1);
        }

        if (!HasFreshHostSelectionReport(candidateState, nowMs))
            summary.DisqualifiedReasons.Add("SelectionReportStale");

        if (GetP95FrameMs(candidateState) > 33f)
            summary.DisqualifiedReasons.Add("FrameBudgetExceeded");

        if (room.UseP2PRelay && !summary.SteamReady)
            summary.DisqualifiedReasons.Add("SteamNotReady");

        var pairCosts = new List<float>();
        var pairRtts = new List<int>();
        int coverageCount = 0;
        bool usedPartialEstimate = false;

        foreach (var peerUid in participatingMembers)
        {
            if (string.Equals(peerUid, candidateUid, StringComparison.OrdinalIgnoreCase))
                continue;

            stateByUid.TryGetValue(peerUid, out var peerState);
            var pair = EvaluatePair(candidateState, peerState, nowMs);
            if (pair.PathType == HostSelectionPathType.Unavailable)
            {
                summary.UnavailablePairCount++;
                continue;
            }

            coverageCount++;
            usedPartialEstimate |= pair.UsedDefaultProxyValues;
            if (pair.PathType == HostSelectionPathType.SteamEligible)
            {
                summary.SteamPairCount++;
                if (pair.UsedMeasuredSteamPairMetrics)
                    summary.MeasuredSteamPairCount++;
                else
                    summary.ProxySteamPairCount++;
            }
            else if (pair.PathType == HostSelectionPathType.ServerRelayComposite)
                summary.ServerRelayPairCount++;

            pairCosts.Add(pair.PairCost);
            pairRtts.Add(pair.AverageRttMs);
        }

        int requiredCoverage = ComputeRequiredCoverage(participatingMembers.Count - 1);
        if (coverageCount < requiredCoverage)
            summary.DisqualifiedReasons.Add("InsufficientPeerCoverage");

        summary.UsesPartialProxyMetrics = usedPartialEstimate;

        return FinalizeCandidate(summary, pairCosts, pairRtts, participatingMembers.Count - 1);
    }

    private static WaitingRoomHostSelectionCandidateDto FinalizeCandidate(
        WaitingRoomHostSelectionCandidateDto summary,
        IReadOnlyList<float> pairCosts,
        IReadOnlyList<int> pairRtts,
        int expectedPeerCount)
    {
        summary.HostCapacityPenalty = CalculateHostCapacityPenalty(summary.P95FrameMs, summary.AvgFrameMs);

        if (pairCosts.Count <= 0)
        {
            summary.AveragePairCost = 0f;
            summary.WorstPairCost = 0f;
            summary.CandidateCost = summary.HostCapacityPenalty;
            summary.AveragePairRttMs = 0;
            summary.WorstPairRttMs = 0;
            summary.IsEligible = summary.DisqualifiedReasons.Count == 0;
            return summary;
        }

        float relayPenalty = ComputePairRatio(summary.ServerRelayPairCount, expectedPeerCount);
        float proxyPenalty = ComputePairRatio(summary.ProxySteamPairCount, expectedPeerCount);
        summary.AveragePairCost = pairCosts.Average();
        summary.WorstPairCost = pairCosts.Max();
        summary.AveragePairRttMs = (int)Math.Round(pairRtts.Average());
        summary.WorstPairRttMs = pairRtts.Max();
        summary.CandidateCost =
            (0.45f * summary.AveragePairCost) +
            (0.20f * summary.WorstPairCost) +
            (0.15f * relayPenalty) +
            (0.10f * proxyPenalty) +
            (0.10f * summary.HostCapacityPenalty);
        summary.IsEligible = summary.DisqualifiedReasons.Count == 0;
        return summary;
    }

    private static void ApplyV2EligibilityAdjustments(
        WaitingRoomDto room,
        IReadOnlyList<WaitingRoomHostSelectionCandidateDto> candidates,
        int participatingMemberCount)
    {
        if (candidates == null || candidates.Count <= 0)
            return;

        int expectedPeerCount = Math.Max(0, participatingMemberCount - 1);
        int requiredMeasuredCoverage = ComputeRequiredCoverage(expectedPeerCount);
        bool hasStrongMeasuredCandidate = candidates.Any(x =>
            x != null
            && x.MeasuredSteamPairCount >= requiredMeasuredCoverage
            && !HasDisqualifyingStateForMeasuredPreference(x));

        foreach (var candidate in candidates)
        {
            if (candidate == null)
                continue;

            if (room.UseP2PRelay
                && hasStrongMeasuredCandidate
                && candidate.MeasuredSteamPairCount <= 0
                && candidate.ServerRelayPairCount >= requiredMeasuredCoverage
                && !candidate.DisqualifiedReasons.Contains("RelayHeavyCandidate"))
            {
                candidate.DisqualifiedReasons.Add("RelayHeavyCandidate");
            }

            candidate.IsEligible = candidate.DisqualifiedReasons.Count == 0;
        }
    }

    private static bool HasDisqualifyingStateForMeasuredPreference(WaitingRoomHostSelectionCandidateDto candidate)
    {
        if (candidate == null || candidate.DisqualifiedReasons == null || candidate.DisqualifiedReasons.Count <= 0)
            return false;

        return candidate.DisqualifiedReasons.Any(reason =>
            string.Equals(reason, "MissingState", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "SelectionReportStale", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "FrameBudgetExceeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(reason, "SteamNotReady", StringComparison.OrdinalIgnoreCase));
    }

    private static HostSelectionPairEvaluation EvaluatePair(
        WaitingRoomMemberTransportDto? candidateState,
        WaitingRoomMemberTransportDto? peerState,
        long nowMs)
    {
        if (candidateState == null || peerState == null)
            return HostSelectionPairEvaluation.Unavailable();

        bool candidateSteam = IsSteamUsable(candidateState);
        bool peerSteam = IsSteamUsable(peerState);

        if (candidateSteam && peerSteam)
        {
            if (TryResolveMeasuredSteamPairRttMs(candidateState, peerState, nowMs, out int measuredRttMs))
            {
                int measuredJitter = Math.Max(4, Math.Max(GetJitterMs(candidateState), GetJitterMs(peerState)) / 2);
                float measuredLoss = Math.Max(GetLossPct(candidateState), GetLossPct(peerState));
                return new HostSelectionPairEvaluation(
                    HostSelectionPathType.SteamEligible,
                    measuredRttMs,
                    Math.Max(measuredRttMs, measuredRttMs + measuredJitter),
                    measuredJitter,
                    measuredLoss,
                    RelayPenalty: 0.02f,
                    UsedDefaultProxyValues: false,
                    UsedMeasuredSteamPairMetrics: true);
            }

            int avgRtt = EstimateSteamPairRttMs(candidateState.CurrentServerRttMs, peerState.CurrentServerRttMs);
            int jitter = Math.Max(GetJitterMs(candidateState), GetJitterMs(peerState));
            float loss = Math.Max(GetLossPct(candidateState), GetLossPct(peerState));
            bool usedDefault = candidateState.CurrentServerRttMs < 0
                || peerState.CurrentServerRttMs < 0
                || candidateState.CurrentServerJitterMs < 0
                || peerState.CurrentServerJitterMs < 0;

            return new HostSelectionPairEvaluation(
                HostSelectionPathType.SteamEligible,
                avgRtt,
                Math.Max(avgRtt, avgRtt + jitter),
                jitter,
                loss,
                RelayPenalty: 0.03f,
                UsedDefaultProxyValues: usedDefault,
                UsedMeasuredSteamPairMetrics: false);
        }

        if (candidateState.CurrentServerRttMs >= 0 && peerState.CurrentServerRttMs >= 0)
        {
            int jitter = Math.Max(GetJitterMs(candidateState), GetJitterMs(peerState));
            float loss = Math.Max(GetLossPct(candidateState), GetLossPct(peerState));
            int avgRtt = candidateState.CurrentServerRttMs + peerState.CurrentServerRttMs + RelayProcessingAllowanceMs;

            return new HostSelectionPairEvaluation(
                HostSelectionPathType.ServerRelayComposite,
                avgRtt,
                Math.Max(avgRtt, avgRtt + jitter),
                jitter,
                loss,
                RelayPenalty: 0.12f,
                UsedDefaultProxyValues: false,
                UsedMeasuredSteamPairMetrics: false);
        }

        return HostSelectionPairEvaluation.Unavailable();
    }

    private static string ResolveMode(
        IReadOnlyList<WaitingRoomHostSelectionCandidateDto> sortedCandidates,
        WaitingRoomHostSelectionCandidateDto? preferredCandidate,
        WaitingRoomDto room)
    {
        if (preferredCandidate == null || !preferredCandidate.IsEligible)
            return "EmergencyFallback";

        int participatingCount = (room.MemberUids ?? new List<string>())
            .Count(uid => !string.IsNullOrWhiteSpace(uid)
                          && (string.Equals(uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase)
                              || !room.MemberReady.TryGetValue(uid, out var ready)
                              || ready));
        int expectedPairs = Math.Max(0, participatingCount - 1);
        bool fullCoverage = expectedPairs == 0
            || (preferredCandidate.SteamPairCount + preferredCandidate.ServerRelayPairCount) >= expectedPairs;

        bool hasMeasuredSteamPairs = preferredCandidate.MeasuredSteamPairCount > 0;
        bool hasProxySteamPairs = preferredCandidate.ProxySteamPairCount > 0;
        bool hasServerRelayPairs = preferredCandidate.ServerRelayPairCount > 0;

        if (!fullCoverage)
            return hasMeasuredSteamPairs ? "PartialMeasured" : "PartialMetrics";

        if (hasMeasuredSteamPairs && !hasProxySteamPairs && !hasServerRelayPairs)
            return "FullMeasured";

        if (hasMeasuredSteamPairs)
            return "HybridMeasured";

        if (hasProxySteamPairs)
            return preferredCandidate.UsesPartialProxyMetrics ? "PartialProxyFallback" : "FullProxy";

        if (preferredCandidate.SteamPairCount > 0 && hasServerRelayPairs)
            return "HybridMixed";

        return "Full";
    }

    private static List<string> BuildOrderedUids(
        string preferredUid,
        IReadOnlyList<WaitingRoomHostSelectionCandidateDto> sortedCandidates,
        WaitingRoomDto room,
        IReadOnlyDictionary<string, WaitingRoomMemberTransportDto> stateByUid,
        IReadOnlyList<string> allMembers)
    {
        if (!sortedCandidates.Any(x => x.IsEligible))
        {
            var fallback = allMembers
                .OrderBy(uid => GetEmergencyFallbackPriority(uid, room, stateByUid))
                .ThenBy(uid => uid, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredUid))
            {
                fallback.RemoveAll(uid => string.Equals(uid, preferredUid, StringComparison.OrdinalIgnoreCase));
                fallback.Insert(0, preferredUid);
            }

            return fallback;
        }

        var ordered = sortedCandidates
            .Select(x => x.Uid)
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredUid))
        {
            ordered.RemoveAll(uid => string.Equals(uid, preferredUid, StringComparison.OrdinalIgnoreCase));
            ordered.Insert(0, preferredUid);
        }

        foreach (var uid in allMembers)
        {
            if (!ordered.Contains(uid, StringComparer.OrdinalIgnoreCase))
                ordered.Add(uid);
        }

        return ordered;
    }

    private static string ResolveEmergencyFallbackUid(
        WaitingRoomDto room,
        IReadOnlyDictionary<string, WaitingRoomMemberTransportDto> stateByUid,
        IReadOnlyList<string> allMembers)
    {
        return allMembers
            .OrderBy(uid => GetEmergencyFallbackPriority(uid, room, stateByUid))
            .ThenBy(uid => uid, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? room.OwnerUid ?? "";
    }

    private static int GetEmergencyFallbackPriority(
        string uid,
        WaitingRoomDto room,
        IReadOnlyDictionary<string, WaitingRoomMemberTransportDto> stateByUid)
    {
        stateByUid.TryGetValue(uid, out var state);
        bool steamReady = IsSteamUsable(state);
        bool isOwner = string.Equals(uid, room.OwnerUid, StringComparison.OrdinalIgnoreCase);

        if (steamReady && isOwner)
            return 0;
        if (steamReady)
            return 1;
        if (isOwner)
            return 2;
        return 3;
    }

    private static int ComputeRequiredCoverage(int peerCount)
    {
        if (peerCount <= 0)
            return 0;

        if (peerCount == 1)
            return 1;

        return (int)Math.Ceiling(peerCount * 0.7d);
    }

    private static bool HasFreshHostSelectionReport(WaitingRoomMemberTransportDto? state, long nowMs)
    {
        if (state == null || state.HostSelectionReportedAtMs <= 0)
            return false;

        return Math.Max(0L, nowMs - state.HostSelectionReportedAtMs) <= ReportFreshnessWindowMs;
    }

    private static bool IsSteamUsable(WaitingRoomMemberTransportDto? state)
    {
        return state != null
               && state.SteamReady
               && !string.IsNullOrWhiteSpace(state.SteamId64);
    }

    private static bool TryResolveMeasuredSteamPairRttMs(
        WaitingRoomMemberTransportDto candidateState,
        WaitingRoomMemberTransportDto peerState,
        long nowMs,
        out int measuredRttMs)
    {
        measuredRttMs = -1;

        int left = FindFreshMeasuredPairRttMs(candidateState, peerState, nowMs);
        int right = FindFreshMeasuredPairRttMs(peerState, candidateState, nowMs);

        if (left >= 0 && right >= 0)
        {
            measuredRttMs = (int)Math.Round((left + right) / 2f);
            return true;
        }

        if (left >= 0)
        {
            measuredRttMs = left;
            return true;
        }

        if (right >= 0)
        {
            measuredRttMs = right;
            return true;
        }

        return false;
    }

    private static int FindFreshMeasuredPairRttMs(
        WaitingRoomMemberTransportDto sourceState,
        WaitingRoomMemberTransportDto peerState,
        long nowMs)
    {
        if (sourceState?.MeasuredSteamPairs == null || sourceState.MeasuredSteamPairs.Count <= 0)
            return -1;

        var match = sourceState.MeasuredSteamPairs
            .Where(x => x != null
                        && x.Connected
                        && x.RttMs >= 0
                        && Math.Max(0L, nowMs - x.ReportedAtMs) <= ReportFreshnessWindowMs
                        && MatchesPeer(x, peerState))
            .OrderByDescending(x => x.ReportedAtMs)
            .ThenBy(x => x.RttMs)
            .FirstOrDefault();

        return match?.RttMs ?? -1;
    }

    private static bool MatchesPeer(WaitingRoomMeasuredSteamPairDto pair, WaitingRoomMemberTransportDto peerState)
    {
        if (pair == null || peerState == null)
            return false;

        if (!string.IsNullOrWhiteSpace(pair.PeerUid)
            && !string.IsNullOrWhiteSpace(peerState.Uid)
            && string.Equals(pair.PeerUid, peerState.Uid, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(pair.PeerSteamId64)
            && !string.IsNullOrWhiteSpace(peerState.SteamId64)
            && string.Equals(pair.PeerSteamId64, peerState.SteamId64, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static int EstimateSteamPairRttMs(int leftServerRttMs, int rightServerRttMs)
    {
        if (leftServerRttMs >= 0 && rightServerRttMs >= 0)
            return Math.Max(leftServerRttMs, rightServerRttMs) + 6;

        if (leftServerRttMs >= 0)
            return leftServerRttMs + 15;

        if (rightServerRttMs >= 0)
            return rightServerRttMs + 15;

        return DefaultSteamPairRttMs;
    }

    private static int GetJitterMs(WaitingRoomMemberTransportDto state)
    {
        return state?.CurrentServerJitterMs >= 0
            ? state.CurrentServerJitterMs
            : DefaultSteamPairJitterMs;
    }

    private static float GetLossPct(WaitingRoomMemberTransportDto state)
    {
        if (state == null)
            return DefaultLossPct;

        return state.CurrentServerLossPct > 0f
            ? state.CurrentServerLossPct
            : DefaultLossPct;
    }

    private static float GetP95FrameMs(WaitingRoomMemberTransportDto state)
    {
        if (state == null)
            return 60f;

        if (state.P95FrameMs > 0f)
            return state.P95FrameMs;

        if (state.AvgFrameMs > 0f)
            return state.AvgFrameMs;

        return 16.7f;
    }

    private static float CalculateHostCapacityPenalty(float p95FrameMs, float avgFrameMs)
    {
        float effectiveP95 = p95FrameMs > 0f
            ? p95FrameMs
            : (avgFrameMs > 0f ? avgFrameMs : 16.7f);
        float normFramePenalty = Clamp01((effectiveP95 - 16.7f) / 16.3f);
        return 0.70f * normFramePenalty;
    }

    private static long GetLatestTransportReportedAtMs(WaitingRoomMemberTransportDto state)
    {
        if (state == null)
            return 0L;

        long latestPairReportedAtMs = state.MeasuredSteamPairs != null && state.MeasuredSteamPairs.Count > 0
            ? state.MeasuredSteamPairs.Max(x => x?.ReportedAtMs ?? 0L)
            : 0L;

        return Math.Max(
            Math.Max(state.HostSelectionReportedAtMs, state.HostProbeReportedAtMs),
            latestPairReportedAtMs);
    }

    private static float ComputePairRatio(int pairCount, int expectedPeerCount)
    {
        if (expectedPeerCount <= 0)
            return 0f;

        return Clamp01(pairCount / (float)expectedPeerCount);
    }

    private static float Clamp01(float value)
    {
        if (value <= 0f)
            return 0f;
        if (value >= 1f)
            return 1f;
        return value;
    }

    private enum HostSelectionPathType
    {
        Unavailable = 0,
        ServerRelayComposite = 1,
        SteamEligible = 2
    }

    private readonly record struct HostSelectionPairEvaluation(
        HostSelectionPathType PathType,
        int AverageRttMs,
        int WorstRttMs,
        int JitterMs,
        float LossPct,
        float RelayPenalty,
        bool UsedDefaultProxyValues,
        bool UsedMeasuredSteamPairMetrics)
    {
        public float PairCost =>
            (0.50f * Clamp01(AverageRttMs / 120f)) +
            (0.20f * Clamp01(WorstRttMs / 180f)) +
            (0.15f * Clamp01(JitterMs / 40f)) +
            (0.10f * Clamp01(LossPct / 3f)) +
            (0.05f * RelayPenalty);

        public static HostSelectionPairEvaluation Unavailable()
            => new(HostSelectionPathType.Unavailable, -1, -1, -1, 100f, 1f, true, false);
    }
}
