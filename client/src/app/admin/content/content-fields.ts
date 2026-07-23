export type ContentGroup = 'home' | 'about' | 'events' | 'gallery' | 'contact' | 'footer';

export interface ContentField {
  key: string;
  group: ContentGroup;
  /** Textarea vs single-line input. */
  multiline?: boolean;
  /** A single value shared across all three languages (e.g. a stat number), not translated. */
  shared?: boolean;
}

/**
 * Drives the admin Content page's layout — grouping, labels, and input type.
 * The backend has no notion of this; it just stores/returns a flat key→value map
 * (see Data/ContentDefaults.cs on the server, which uses the same key names).
 */
export const CONTENT_FIELDS: ContentField[] = [
  { key: 'home.heroTitle', group: 'home' },
  { key: 'home.heroSubtitle', group: 'home', multiline: true },
  { key: 'home.heroCta', group: 'home' },
  { key: 'home.heroSecondary', group: 'home' },
  { key: 'home.connect', group: 'home' },
  { key: 'home.connectText', group: 'home', multiline: true },
  { key: 'home.showcase', group: 'home' },
  { key: 'home.showcaseText', group: 'home', multiline: true },
  { key: 'home.build', group: 'home' },
  { key: 'home.buildText', group: 'home', multiline: true },
  { key: 'home.statsTitle', group: 'home' },
  { key: 'home.statAttendees', group: 'home' },
  { key: 'home.statAttendeesValue', group: 'home', shared: true },
  { key: 'home.statViews', group: 'home' },
  { key: 'home.statViewsValue', group: 'home', shared: true },
  { key: 'home.statEvents', group: 'home' },
  { key: 'home.statEventsValue', group: 'home', shared: true },
  { key: 'home.statMeals', group: 'home' },
  { key: 'home.statMealsValue', group: 'home', shared: true },
  { key: 'home.upcomingTitle', group: 'home' },
  { key: 'home.missionTitle', group: 'home' },
  { key: 'home.missionText', group: 'home', multiline: true },

  { key: 'about.title', group: 'about' },
  { key: 'about.intro', group: 'about', multiline: true },
  { key: 'about.founding', group: 'about', multiline: true },
  { key: 'about.open', group: 'about', multiline: true },
  { key: 'about.missionTitle', group: 'about' },
  { key: 'about.missionText', group: 'about', multiline: true },
  { key: 'about.valuesTitle', group: 'about' },
  { key: 'about.valuePride', group: 'about' },
  { key: 'about.valuePrideText', group: 'about', multiline: true },
  { key: 'about.valueOpenness', group: 'about' },
  { key: 'about.valueOpennessText', group: 'about', multiline: true },
  { key: 'about.valueService', group: 'about' },
  { key: 'about.valueServiceText', group: 'about', multiline: true },
  { key: 'about.valueExcellence', group: 'about' },
  { key: 'about.valueExcellenceText', group: 'about', multiline: true },
  { key: 'about.valueUnity', group: 'about' },
  { key: 'about.valueUnityText', group: 'about', multiline: true },
  { key: 'about.socialTitle', group: 'about' },
  { key: 'about.socialText', group: 'about', multiline: true },
  { key: 'about.teamTitle', group: 'about' },
  { key: 'about.teamNote', group: 'about', multiline: true },
  { key: 'about.joinCta', group: 'about' },

  { key: 'events.intro', group: 'events', multiline: true },

  { key: 'gallery.intro', group: 'gallery', multiline: true },

  { key: 'contact.intro', group: 'contact', multiline: true },
  { key: 'contact.partnershipText', group: 'contact', multiline: true },
  { key: 'contact.volunteerText', group: 'contact', multiline: true },

  { key: 'footer.tagline', group: 'footer' },
  { key: 'footer.location', group: 'footer' },
  { key: 'footer.rights', group: 'footer', multiline: true }
];

export const CONTENT_GROUPS: ContentGroup[] = ['home', 'about', 'events', 'gallery', 'contact', 'footer'];
