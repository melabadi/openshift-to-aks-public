using System;
using System.Collections.Generic;
using System.Globalization;

namespace Claims
{
    internal static class ClaimAnalysis
    {
        // Money is expressed in integer USD cents, never floating-point dollars.
        public static object Analyze(IDictionary<string, object> input)
        {
            if (input == null || input.Count != 4)
                throw new ArgumentException("Supply lossCents, deductibleCents, limitCents and daysToReport.");
            int loss = Integer(input, "lossCents", 1, 1000000000);
            int deductible = Integer(input, "deductibleCents", 0, 1000000000);
            int limit = Integer(input, "limitCents", 1, 1000000000);
            int days = Integer(input, "daysToReport", 0, 3650);
            var reasons = new List<string>();
            int net = Math.Max(0, loss - deductible);
            if (net == 0) reasons.Add("WITHIN_DEDUCTIBLE");
            if (net > limit) reasons.Add("POLICY_LIMIT_APPLIED");
            if (days > 30) reasons.Add("LATE_REPORT");
            return new {
                estimatedPayableCents = Math.Min(net, limit),
                queue = reasons.Count == 0 ? "standard" : "adjuster-review",
                reasons
            };
        }

        private static int Integer(IDictionary<string, object> input, string name, int min, int max)
        {
            object value;
            if (!input.TryGetValue(name, out value) || !(value is int) ||
                (int)value < min || (int)value > max)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    "{0} must be an integer between {1} and {2}.", name, min, max));
            return (int)value;
        }
    }
}
