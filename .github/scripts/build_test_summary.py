#!/usr/bin/env python3
"""Parse dotnet TRX files and build a sticky PR comment summary."""
# smoke-test: trigger CI to verify the sticky comment workflow
from __future__ import annotations

import os
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
MARKER = "<!-- ci-test-summary -->"


@dataclass
class Section:
    name: str
    outcome: str  # success | failure | skipped | missing
    total: int = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    duration_s: float = 0.0
    failures: list[tuple[str, str]] = None  # (test name, message)

    def __post_init__(self):
        if self.failures is None:
            self.failures = []

    @property
    def status_icon(self) -> str:
        if self.outcome == "success":
            return "✅ Passed"
        if self.outcome == "skipped":
            return "⏭️ Skipped"
        if self.outcome == "missing":
            return "⚠️ Not run"
        return "❌ Failed"

    @property
    def tests_cell(self) -> str:
        if self.outcome == "missing":
            return "—"
        parts = [f"{self.passed} passed"]
        if self.failed:
            parts.append(f"{self.failed} failed")
        if self.skipped:
            parts.append(f"{self.skipped} skipped")
        return ", ".join(parts)

    @property
    def time_cell(self) -> str:
        if self.outcome == "missing" or self.duration_s == 0:
            return "—"
        if self.duration_s < 1:
            return f"{self.duration_s * 1000:.0f}ms"
        if self.duration_s < 60:
            return f"{self.duration_s:.1f}s"
        m, s = divmod(self.duration_s, 60)
        return f"{int(m)}m{int(s)}s"


def parse_duration(s: str | None) -> float:
    """TRX duration is HH:MM:SS.fffffff."""
    if not s:
        return 0.0
    try:
        h, m, sec = s.split(":")
        return int(h) * 3600 + int(m) * 60 + float(sec)
    except Exception:
        return 0.0


def parse_trx(path: Path, name: str, outcome: str) -> Section:
    if not path.exists():
        return Section(name=name, outcome="missing")

    tree = ET.parse(path)
    root = tree.getroot()

    counters = root.find(".//t:ResultSummary/t:Counters", NS)
    total = int(counters.get("total", 0)) if counters is not None else 0
    passed = int(counters.get("passed", 0)) if counters is not None else 0
    failed = int(counters.get("failed", 0)) if counters is not None else 0
    not_executed = int(counters.get("notExecuted", 0)) if counters is not None else 0

    times = root.find("t:Times", NS)
    duration = 0.0
    if times is not None:
        start = times.get("start")
        finish = times.get("finish")
        if start and finish:
            try:
                # Normalize fractional seconds to 6 digits for fromisoformat
                def norm(ts: str) -> str:
                    if "." in ts:
                        head, tail = ts.split(".", 1)
                        # tail may have timezone suffix
                        frac = ""
                        tz = ""
                        for i, ch in enumerate(tail):
                            if ch.isdigit():
                                frac += ch
                            else:
                                tz = tail[i:]
                                break
                        frac = (frac + "000000")[:6]
                        ts = f"{head}.{frac}{tz}"
                    return ts.replace("Z", "+00:00")

                duration = (
                    datetime.fromisoformat(norm(finish))
                    - datetime.fromisoformat(norm(start))
                ).total_seconds()
            except Exception:
                duration = 0.0

    failures: list[tuple[str, str]] = []
    for utr in root.findall(".//t:UnitTestResult", NS):
        if utr.get("outcome") == "Failed":
            test_name = utr.get("testName", "(unknown)")
            msg_el = utr.find(".//t:Message", NS)
            msg = (msg_el.text or "").strip() if msg_el is not None else ""
            failures.append((test_name, msg))

    final_outcome = "success" if outcome == "success" and failed == 0 else "failure"
    if outcome == "skipped":
        final_outcome = "skipped"

    return Section(
        name=name,
        outcome=final_outcome,
        total=total,
        passed=passed,
        failed=failed,
        skipped=not_executed,
        duration_s=duration,
        failures=failures,
    )


def build_markdown(sections: list[Section]) -> str:
    total = sum(s.total for s in sections if s.outcome != "missing")
    passed = sum(s.passed for s in sections if s.outcome != "missing")
    any_failed = any(s.outcome == "failure" for s in sections)
    any_missing = any(s.outcome == "missing" for s in sections)

    if any_failed:
        overall_icon = "❌"
        overall_text = f"Some checks failed — {passed}/{total} tests passed"
    elif any_missing:
        overall_icon = "⚠️"
        overall_text = f"Incomplete — {passed}/{total} tests passed"
    else:
        overall_icon = "✅"
        overall_text = f"All checks passed — {passed}/{total} tests passed"

    commit = os.environ.get("COMMIT_SHA", "")[:7] or "—"
    run_url = os.environ.get("RUN_URL", "")
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")

    lines = [
        MARKER,
        "## 🧪 CI Test Results — .NET 10",
        "",
        f"**Overall:** {overall_icon} {overall_text}",
        "",
        "| Check | Status | Tests | Time |",
        "|---|---|---|---|",
    ]
    for s in sections:
        lines.append(f"| {s.name} | {s.status_icon} | {s.tests_cell} | {s.time_cell} |")

    lines.append("")
    lines.append(f"📌 **Commit:** `{commit}` | [View Run]({run_url})")
    lines.append(f"🕐 **Updated:** {now}")

    failure_blocks = []
    for s in sections:
        if s.failures:
            failure_blocks.append(f"\n<details><summary>❌ {s.name} failures ({len(s.failures)})</summary>\n")
            for test_name, msg in s.failures[:20]:
                short_msg = msg.splitlines()[0][:300] if msg else ""
                failure_blocks.append(f"\n- **{test_name}**")
                if short_msg:
                    failure_blocks.append(f"  ```\n  {short_msg}\n  ```")
            if len(s.failures) > 20:
                failure_blocks.append(f"\n_…and {len(s.failures) - 20} more_")
            failure_blocks.append("\n</details>")

    if failure_blocks:
        lines.append("")
        lines.extend(failure_blocks)

    return "\n".join(lines) + "\n"


def main() -> int:
    sections = [
        parse_trx(
            Path(os.environ.get("UNIT_TRX", "TestResults/unit/unit.trx")),
            "Unit Tests",
            os.environ.get("UNIT_OUTCOME", "missing"),
        ),
        parse_trx(
            Path(os.environ.get("E2E_TRX", "TestResults/e2e/e2e.trx")),
            "E2E Tests",
            os.environ.get("E2E_OUTCOME", "missing"),
        ),
    ]

    out = Path("TestResults/summary.md")
    out.parent.mkdir(parents=True, exist_ok=True)
    md = build_markdown(sections)
    out.write_text(md, encoding="utf-8")

    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as f:
            f.write(md)

    print(md)
    return 0


if __name__ == "__main__":
    sys.exit(main())
