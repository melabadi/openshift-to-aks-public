"use strict";

const form = document.getElementById("claim-form");
const fields = document.getElementById("fields");
const analyze = document.getElementById("analyze");
const panel = document.getElementById("analysis");
const result = document.getElementById("result");
const empty = document.getElementById("empty");
const error = document.getElementById("error");
const status = document.getElementById("status");
const moneyFields = ["loss", "deductible", "limit"].map(id => document.getElementById(id));
const days = document.getElementById("days");
const currency = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const explanations = {
  WITHIN_DEDUCTIBLE: ["Within the deductible", "The deductible covers the reported loss, so no payout is estimated."],
  POLICY_LIMIT_APPLIED: ["Policy limit applied", "The loss after the deductible exceeds the policy limit. The estimate is capped."],
  LATE_REPORT: ["Late reporting", "The claim was reported more than 30 days after the loss."]
};
const samples = {
  standard: ["2300.00", "500.00", "5000.00", "7"],
  review: ["9000.00", "500.00", "5000.00", "31"],
  deductible: ["500.00", "500.00", "5000.00", "0"]
};

function clearResult(message) {
  result.hidden = true;
  empty.hidden = false;
  error.hidden = true;
  status.textContent = message;
}

for (const input of [...moneyFields, days]) {
  input.addEventListener("input", () => {
    input.setCustomValidity("");
    clearResult("Details changed. Analyze the claim for an updated estimate.");
  });
}

for (const button of document.querySelectorAll("[data-sample]")) {
  button.addEventListener("click", () => {
    [...moneyFields, days].forEach((input, index) => {
      input.value = samples[button.dataset.sample][index];
      input.setCustomValidity("");
    });
    clearResult("Example loaded. Select Analyze claim to submit it.");
  });
}

function cents(input) {
  // Split the decimal text instead of rounding floating-point dollars.
  const [whole, fraction = ""] = input.value.split(".");
  return Number(whole || "0") * 100 + Number(fraction.padEnd(2, "0"));
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  for (const input of moneyFields) {
    input.setCustomValidity(/^(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$/.test(input.value)
      ? "" : "Enter US dollars with at most two decimal places, without exponent notation.");
  }
  if (!form.reportValidity()) return;

  const claim = {
    lossCents: cents(moneyFields[0]),
    deductibleCents: cents(moneyFields[1]),
    limitCents: cents(moneyFields[2]),
    daysToReport: Number(days.value)
  };
  clearResult("Analyzing claim.");
  empty.hidden = true;
  panel.setAttribute("aria-busy", "true");
  fields.disabled = true;
  analyze.textContent = "Analyzing...";
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const response = await fetch("/claims/analyze", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(claim),
      signal: controller.signal
    });
    if (!response.ok) throw new Error(`The service could not analyze this claim (HTTP ${response.status}). Please try again.`);
    const data = await response.json();
    if (!data || !Number.isInteger(data.estimatedPayableCents) || data.estimatedPayableCents < 0 ||
        data.estimatedPayableCents > claim.limitCents ||
        !["standard", "adjuster-review"].includes(data.queue) ||
        !Array.isArray(data.reasons) || data.reasons.some(reason => !Object.hasOwn(explanations, reason))) {
      throw new Error("The service returned an unexpected result. Please try again.");
    }
    const payable = currency.format(data.estimatedPayableCents / 100);
    const queue = document.getElementById("queue");
    document.getElementById("payable").textContent = payable;
    queue.textContent = data.queue === "standard" ? "Standard review" : "Adjuster review";
    queue.classList.toggle("review", data.queue === "adjuster-review");
    const reasons = document.getElementById("reasons");
    reasons.replaceChildren();
    const descriptions = data.reasons.length ? data.reasons.map(reason => explanations[reason])
      : [["No review flags", "The deductible and policy limit were applied. No additional review flags were raised."]];
    for (const [title, description] of descriptions) {
      const item = document.createElement("li");
      const heading = document.createElement("strong");
      heading.textContent = title;
      item.append(heading, document.createTextNode(description));
      reasons.append(item);
    }
    result.hidden = false;
    status.textContent = `Estimated payable ${payable}. ${queue.textContent}.`;
  } catch (failure) {
    error.textContent = failure.name === "AbortError"
      ? "The analysis timed out. Please try again."
      : failure instanceof TypeError ? "Unable to reach the service. Check your connection and try again." : failure.message;
    error.hidden = false;
    status.textContent = "Analysis failed. No estimate is available.";
  } finally {
    clearTimeout(timeout);
    panel.setAttribute("aria-busy", "false");
    fields.disabled = false;
    analyze.textContent = "Analyze claim";
  }
});

fields.disabled = false;
