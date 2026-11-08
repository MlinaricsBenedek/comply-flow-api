using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using comply_flow_api.Models;

namespace comply_flow_api.Services;

public enum TransferFeeAssessmentStatus
{
    InsufficientData,
    TariffMismatchReported,
    TariffBelowPublished,
    TariffMatches
}

public sealed record TransferFeeComplaintAssessment(
    TransferFeeAssessmentStatus Status,
    decimal? TransferAmountHuf,
    decimal? ChargedFeeHuf,
    decimal? ExpectedFeeHuf,
    decimal? DifferenceHuf,
    IReadOnlyList<string> MissingInformation,
    string CustomerMessage);

public static partial class TransferFeeComplaintEvaluator
{
    private const string RuleId = "HU-BANK-TRANSFER-FEES-001";

    public static TransferFeeComplaintAssessment? Evaluate(
        string bank,
        string message,
        Rule? selectedRule)
    {
        if (!string.Equals(bank, "otp", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(selectedRule?.Id, RuleId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalizedMessage = RemoveDiacritics(message).ToLowerInvariant();
        var missingInformation = new List<string>();
        if (!normalizedMessage.Contains("forint megtakaritasi szamla", StringComparison.Ordinal) &&
            !normalizedMessage.Contains("huf megtakaritasi szamla", StringComparison.Ordinal))
        {
            missingInformation.Add("a terhelt számla OTP forint megtakarítási számla-e");
        }

        if (!ContainsTransferChannel(normalizedMessage))
        {
            missingInformation.Add("az utalás mobilbanki vagy internetbanki csatornája");
        }

        if (!normalizedMessage.Contains("eseti", StringComparison.Ordinal) ||
            !ContainsInternalNonOwnTransfer(normalizedMessage))
        {
            missingInformation.Add("hogy eseti, OTP-n belüli, nem saját számlára indított utalásról van-e szó");
        }

        var moneyValues = FindMoneyValues(message);
        var transferValues = moneyValues
            .Where(item => item.Kind == MoneyValueKind.Transfer)
            .ToArray();
        var chargedFeeValues = moneyValues
            .Where(item => item.Kind == MoneyValueKind.ChargedFee)
            .ToArray();
        if (transferValues.Length > 1)
        {
            missingInformation.Add("melyik összeg az érintett átutalás összege");
        }

        if (chargedFeeValues.Length > 1)
        {
            missingInformation.Add("melyik összeg az érintett átutalás ténylegesen levont díja");
        }

        var transferAmount = transferValues
            .Select(item => (decimal?)item.Amount)
            .FirstOrDefault();
        var chargedFee = chargedFeeValues
            .Select(item => (decimal?)item.Amount)
            .FirstOrDefault();

        if (transferAmount is null)
        {
            missingInformation.Add("az átutalt összeg");
        }

        if (chargedFee is null)
        {
            missingInformation.Add("a ténylegesen levont átutalási díj");
        }

        if (missingInformation.Count > 0)
        {
            var missing = string.Join(", ", missingInformation);
            return new TransferFeeComplaintAssessment(
                TransferFeeAssessmentStatus.InsufficientData,
                transferAmount,
                chargedFee,
                null,
                null,
                missingInformation,
                $"A díj jogossága a jelenleg megadott adatokból nem állapítható meg. A díjtétel összevetéséhez még szükség van erre: {missing}. Kérjük, pontosítsa ezeket az adatokat.");
        }

        var tariff = GetTariff(selectedRule!);
        var calculatedFee = transferAmount!.Value * tariff.RatePercent / 100m;
        var expectedFee = Math.Clamp(calculatedFee, tariff.MinimumFeeHuf, tariff.MaximumFeeHuf);
        if (expectedFee != decimal.Truncate(expectedFee))
        {
            const string roundingMessage =
                "A hirdetményben szereplő százalékos díj alkalmazásakor a kerekítés szabálya is szükséges az egyforintos pontosságú összevetéshez; ezért az eltérést nem minősítem automatikusan.";
            return new TransferFeeComplaintAssessment(
                TransferFeeAssessmentStatus.InsufficientData,
                transferAmount,
                chargedFee,
                expectedFee,
                null,
                ["a díjszámításra alkalmazott kerekítési szabály"],
                roundingMessage);
        }

        var difference = chargedFee!.Value - expectedFee;
        if (difference != 0)
        {
            return new TransferFeeComplaintAssessment(
                difference > 0
                    ? TransferFeeAssessmentStatus.TariffMismatchReported
                    : TransferFeeAssessmentStatus.TariffBelowPublished,
                transferAmount,
                chargedFee,
                expectedFee,
                difference,
                [],
                difference > 0
                    ? $"A megadott adatok és a 2026. július 1-jétől hatályos OTP-hirdetmény alapján a {transferAmount.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft-os utalás díja {expectedFee.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft. Ön {chargedFee.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft terhelést jelzett, ami {difference.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft-tal magasabb. A díjjal kapcsolatos kifogás a megadott tarifához képest megalapozottnak tűnik, ezért a tételes terhelés kivizsgálása indokolt. A banki rendszer hibája a közölt adatokból önmagában nem igazolható; elnézést csak a hibás terhelés ellenőrzése után indokolt kérni."
                    : $"A megadott adatok és a 2026. július 1-jétől hatályos OTP-hirdetmény alapján a {transferAmount.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft-os utalás díja {expectedFee.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft, Ön pedig {chargedFee.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft terhelést jelzett. A jelzett összeg alacsonyabb a hirdetmény alapján számított díjnál, ezért a túlszámlázásra vonatkozó kifogást a megadott adatok nem támasztják alá. Az ettől eltérő terhelési tételek ellenőrzéséhez a tételes számlakivonat szükséges.");
        }

        return new TransferFeeComplaintAssessment(
            TransferFeeAssessmentStatus.TariffMatches,
            transferAmount,
            chargedFee,
            expectedFee,
            difference,
            [],
            $"A megadott adatok alapján a {transferAmount.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft-os utalás hirdetmény szerinti díja {expectedFee.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft, a jelzett terhelés pedig {chargedFee.Value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))} Ft. Ezek az összegek egyeznek; a díj összegével szembeni kifogást a megadott tarifaadat nem támasztja alá. Ha a terhelés más díjtételt is tartalmaz, annak tételes ellenőrzése szükséges.");
    }

    private static (decimal RatePercent, decimal MinimumFeeHuf, decimal MaximumFeeHuf) GetTariff(Rule rule)
    {
        if (!rule.Result.AdditionalData.TryGetValue("transferFeeAssessment", out var tariff) ||
            tariff.ValueKind != JsonValueKind.Object ||
            !tariff.TryGetProperty("ratePercent", out var rate) ||
            !rate.TryGetDecimal(out var ratePercent) ||
            !tariff.TryGetProperty("minimumFeeHuf", out var minimum) ||
            !minimum.TryGetDecimal(out var minimumFeeHuf) ||
            !tariff.TryGetProperty("maximumFeeHuf", out var maximum) ||
            !maximum.TryGetDecimal(out var maximumFeeHuf) ||
            ratePercent <= 0 ||
            minimumFeeHuf < 0 ||
            maximumFeeHuf < minimumFeeHuf)
        {
            throw new InvalidDataException(
                $"Rule '{rule.Id}' has no valid transferFeeAssessment tariff.");
        }

        return (ratePercent, minimumFeeHuf, maximumFeeHuf);
    }

    private static List<(decimal Amount, MoneyValueKind Kind)> FindMoneyValues(string message)
    {
        var values = new List<(decimal Amount, MoneyValueKind Kind)>();
        foreach (Match match in MoneyAmount().Matches(message))
        {
            var amountText = Regex.Replace(match.Groups["amount"].Value, @"[\s\u00a0]", string.Empty);
            amountText = amountText.Replace(".", string.Empty, StringComparison.Ordinal);
            amountText = amountText.Replace(',', '.');
            if (!decimal.TryParse(
                    amountText,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                continue;
            }

            values.Add((amount, ClassifyMoneyAmount(message, match)));
        }

        return values;
    }

    private static MoneyValueKind ClassifyMoneyAmount(string message, Match amountMatch)
    {
        var separators = new[] { '.', ';', '!', '?', '\r', '\n' };
        var previousSeparator = message.LastIndexOfAny(separators, amountMatch.Index);
        var start = previousSeparator < 0 ? 0 : previousSeparator + 1;
        var nextSeparator = message.IndexOfAny(separators, amountMatch.Index + amountMatch.Length);
        var end = nextSeparator < 0 ? message.Length : nextSeparator;
        var length = end - start;
        var context = message.Substring(start, length);
        var amountStart = amountMatch.Index - start;
        var amountEnd = amountStart + amountMatch.Length;
        var expectedDistance = GetNearestMarkerDistance(ExpectedFeeMarkers(), context, amountStart, amountEnd);
        var chargedDistance = GetNearestMarkerDistance(ChargedFeeMarkers(), context, amountStart, amountEnd);
        var transferDistance = GetNearestMarkerDistance(TransferMarkers(), context, amountStart, amountEnd);

        if (expectedDistance <= chargedDistance && expectedDistance <= transferDistance)
        {
            return MoneyValueKind.ExpectedFee;
        }

        if (chargedDistance <= transferDistance)
        {
            return MoneyValueKind.ChargedFee;
        }

        return transferDistance < int.MaxValue
            ? MoneyValueKind.Transfer
            : MoneyValueKind.Other;
    }

    private static int GetNearestMarkerDistance(
        Regex markers,
        string context,
        int amountStart,
        int amountEnd)
    {
        var nearestDistance = int.MaxValue;
        foreach (Match marker in markers.Matches(context))
        {
            var distance = marker.Index >= amountEnd
                ? marker.Index - amountEnd
                : amountStart >= marker.Index + marker.Length
                    ? amountStart - (marker.Index + marker.Length)
                    : 0;
            nearestDistance = Math.Min(nearestDistance, distance);
        }

        return nearestDistance;
    }

    private enum MoneyValueKind
    {
        Other,
        Transfer,
        ChargedFee,
        ExpectedFee
    }

    private static bool ContainsTransferChannel(string message) =>
        message.Contains("mobilbank", StringComparison.Ordinal) ||
        message.Contains("mobil bank", StringComparison.Ordinal) ||
        message.Contains("internetbank", StringComparison.Ordinal) ||
        message.Contains("internet bank", StringComparison.Ordinal);

    private static bool ContainsInternalNonOwnTransfer(string message) =>
        message.Contains("nem sajat szaml", StringComparison.Ordinal) &&
        (message.Contains("otp ugyfel", StringComparison.Ordinal) ||
         message.Contains("masik otp", StringComparison.Ordinal) ||
         message.Contains("otp-n belul", StringComparison.Ordinal) ||
         message.Contains("otp-n beluli", StringComparison.Ordinal) ||
         message.Contains("bankon belul", StringComparison.Ordinal));

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(
        @"(?<amount>\d(?:[\d\s\u00a0.]*\d)?(?:,\d{1,2})?)\s*(?:Ft\b|forint(?:ot|ért|ra|ban|ból|ról|tal)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyAmount();

    [GeneratedRegex(
        @"(?:át)?utal(?:tam|t|tunk|tok|ni)|(?:át)?utalás\b|megb[ií]z[aá]s\w*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TransferMarkers();

    [GeneratedRegex(
        @"d[ií]j\w*|k[oö]lts[eé]g\w*|levon\w*|vont\w*|terhel\w*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ChargedFeeMarkers();

    [GeneratedRegex(
        @"sz[aá]m[ií]t\w*|v[aá]r\w*|kellett\s+volna",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExpectedFeeMarkers();
}
