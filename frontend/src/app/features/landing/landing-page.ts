import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RevealOnScrollDirective } from '../../shared/reveal-on-scroll.directive';

interface FlowStage {
  readonly number: string;
  readonly title: string;
  readonly description: string;
  readonly status: 'completed' | 'active' | 'upcoming';
}

interface TrustPillar {
  readonly tier: string;
  readonly badge: string;
  readonly title: string;
  readonly subtitle: string;
  readonly description: string;
  readonly accentVar: '--lp-accent' | '--lp-teal' | '--lp-info';
}

interface NavLink {
  readonly label: string;
  readonly targetId: string;
}

interface AssistantSample {
  readonly question: string;
  readonly toolCalled: string;
  readonly answer: string;
  readonly verifiedData: string;
}

/**
 * Public marketing entry point at `/`.
 *
 * Direction A' — Light Premium Fintech.
 * Static and presentation-only: no HTTP calls, no auth, no backend dependencies.
 * All illustrative numbers and mock assistant turns are labeled as demo/illustration data.
 */
@Component({
  selector: 'app-landing-page',
  imports: [RouterLink, RevealOnScrollDirective],
  templateUrl: './landing-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {
  protected readonly navLinks: readonly NavLink[] = [
    { label: 'Product', targetId: 'preview' },
    { label: 'How it works', targetId: 'workflow' },
    { label: 'Trust', targetId: 'trust' },
    { label: 'AI Assistant', targetId: 'ai-assistant' },
  ];

  /**
   * Complete 6-step product reconciliation workflow.
   */
  protected readonly workflowSteps: readonly FlowStage[] = [
    {
      number: '01',
      title: 'Upload',
      description: 'Payment, Bank and Settlement files.',
      status: 'completed',
    },
    {
      number: '02',
      title: 'Normalize',
      description: 'Unified standard format across all sources.',
      status: 'completed',
    },
    {
      number: '03',
      title: 'Reconcile',
      description: 'Deterministic rules match & classify records.',
      status: 'active',
    },
    {
      number: '04',
      title: 'Verify',
      description: 'Checked against independent ground truth.',
      status: 'upcoming',
    },
    {
      number: '05',
      title: 'Investigate',
      description: 'Exceptions and variances flagged for review.',
      status: 'upcoming',
    },
    {
      number: '06',
      title: 'Explain',
      description: 'Grounded AI explains breaks over verified data.',
      status: 'upcoming',
    },
  ];

  /**
   * Explicit trust hierarchy: Deterministic Engine -> Ground Truth -> AI Investigation.
   */
  protected readonly trustPillars: readonly TrustPillar[] = [
    {
      tier: '01 / SOURCE OF TRUTH',
      badge: 'Deterministic Engine',
      title: 'Authoritative reconciliation result',
      subtitle: 'Rule-based execution',
      description:
        'Every match, mismatch and exception is produced by rule-based reconciliation logic — computed the same way every time. Never guessed by a model.',
      accentVar: '--lp-accent',
    },
    {
      tier: '02 / INDEPENDENT CHECK',
      badge: 'Ground Truth',
      title: 'Independent verification',
      subtitle: 'Post-run verification',
      description:
        'An independently generated expected result checks the engine’s output after the fact, so "correct" is measured, not assumed.',
      accentVar: '--lp-teal',
    },
    {
      tier: '03 / AUDIT ASSISTANT',
      badge: 'AI Investigation',
      title: 'Explanation over verified data',
      subtitle: 'Read-only grounded AI',
      description:
        'AI explains and helps investigate results already produced by the engine, using grounded, read-only tools. It never decides the financial truth.',
      accentVar: '--lp-info',
    },
  ];

  protected readonly exampleExceptions: readonly string[] = [
    'TXN-0098',
    'TXN-0099',
    'TXN-0100',
  ];

  protected readonly suggestedQuestions: readonly string[] = [
    'What is our match rate?',
    'Which exceptions need attention?',
    'Show unmatched transactions',
    'Explain TXN-0099',
  ];

  private readonly sampleData: Record<string, AssistantSample> = {
    'What is our match rate?': {
      question: 'What is our match rate?',
      toolCalled: 'getReconciliationSummary',
      verifiedData: '97.00% (97 of 100 transactions matched)',
      answer: 'Reconciliation completed with 97 matches and 3 exceptions across Payment, Bank, and Settlement files.',
    },
    'Which exceptions need attention?': {
      question: 'Which exceptions need attention?',
      toolCalled: 'getUnmatchedRecords',
      verifiedData: '3 unresolved exceptions (TXN-0098, TXN-0099, TXN-0100)',
      answer: '3 exceptions require review. TXN-0098 and TXN-0099 are missing Bank records; TXN-0100 has an amount mismatch (₹12,540 vs ₹12,500).',
    },
    'Show unmatched transactions': {
      question: 'Show unmatched transactions',
      toolCalled: 'getUnmatchedRecords',
      verifiedData: '3 records flagged for exception investigation',
      answer: 'Identified 2 MISSING_BANK_RECORD items and 1 AMOUNT_MISMATCH in current run batch-2026-08.',
    },
    'Explain TXN-0099': {
      question: 'Explain TXN-0099',
      toolCalled: 'explainException',
      verifiedData: 'Payment ₹12,540 found; Bank statement missing corresponding entry',
      answer: 'Payment was captured on Gateway at 14:32:10 UTC, but no corresponding ledger entry was returned in the EOD bank settlement extract.',
    },
  };

  protected readonly activeQuestion = signal<string>('Which exceptions need attention?');

  protected get currentSample(): AssistantSample {
    return this.sampleData[this.activeQuestion()] ?? this.sampleData['Which exceptions need attention?'];
  }

  protected selectQuestion(question: string): void {
    this.activeQuestion.set(question);
  }

  protected scrollToId(targetId: string): void {
    const target = document.getElementById(targetId);

    if (!target) {
      return;
    }

    const prefersReducedMotion = window.matchMedia?.(
      '(prefers-reduced-motion: reduce)',
    ).matches;

    target.scrollIntoView({
      behavior: prefersReducedMotion ? 'auto' : 'smooth',
      block: 'start',
    });
  }
}

