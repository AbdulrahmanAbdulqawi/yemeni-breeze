import { FeedbackAnswers } from '../../core/models';

/**
 * The choices on the feedback form, as option codes. Labels live in i18n under
 * `feedback.options.<group>.<code>`. Must match FeedbackOptions on the server.
 */
export const FEEDBACK_OPTIONS = {
  expectations: ['people', 'culture', 'food', 'heritage', 'belonging', 'fun', 'other'],
  met: ['yes', 'partly', 'no'],
  join: ['yes', 'maybe', 'no'],
  contributions: ['organise', 'cooking', 'traditions', 'performing', 'workshops', 'media', 'volunteering', 'other'],
  music: ['traditional', 'sanaani', 'lahji', 'hadhrami', 'modern', 'mix'],
  dances: ['baraa', 'lahji', 'folk', 'live', 'poetry', 'other']
} as const;

export type FeedbackListKey = 'expectations' | 'contributions' | 'music' | 'dances';
export type FeedbackTextKey = {
  [K in keyof FeedbackAnswers]: FeedbackAnswers[K] extends string | null ? K : never
}[keyof FeedbackAnswers];

export function emptyAnswers(): FeedbackAnswers {
  return {
    expectations: [], expectationsOther: null, metExpectations: null, improvements: null,
    favouritePart: null, joinCommunity: null, contributions: [], contributionsOther: null,
    dishes: null, regionalFood: null, music: [], dances: [], dancesOther: null,
    traditions: null, missing: null, engagement: null, ideas: null, anythingElse: null
  };
}

/** The 15 questions in order, for the admin view: which answer field, and how to show it. */
export const FEEDBACK_QUESTIONS: {
  n: number;
  field: keyof FeedbackAnswers;
  options?: keyof typeof FEEDBACK_OPTIONS;
  other?: FeedbackTextKey;
}[] = [
  { n: 1, field: 'expectations', options: 'expectations', other: 'expectationsOther' },
  { n: 2, field: 'metExpectations', options: 'met' },
  { n: 3, field: 'improvements' },
  { n: 4, field: 'favouritePart' },
  { n: 5, field: 'joinCommunity', options: 'join' },
  { n: 6, field: 'contributions', options: 'contributions', other: 'contributionsOther' },
  { n: 7, field: 'dishes' },
  { n: 8, field: 'regionalFood' },
  { n: 9, field: 'music', options: 'music' },
  { n: 10, field: 'dances', options: 'dances', other: 'dancesOther' },
  { n: 11, field: 'traditions' },
  { n: 12, field: 'missing' },
  { n: 13, field: 'engagement' },
  { n: 14, field: 'ideas' },
  { n: 15, field: 'anythingElse' }
];
