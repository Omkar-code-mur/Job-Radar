export type NormalizedJob = {
  title: string;
  description: string;
  location: string;
  workplaceType: string;
  postedDate: string;
};

export type CandidateProfile = {
  roles: string[];
  skills: string[];
  technologies: string[];
  minYears: number;
  maxYears: number;
  locations: string[];
  workplacePreference: string;
  includeKeywords: string[];
  excludeKeywords: string[];
};

export type MatchResult = {
  score: number;
  matchedCriteria: string[];
  missingCriteria: string[];
  reasons: string[];
  breakdown: {
    role: number;
    skills: number;
    experience: number;
    location: number;
    aiRelevance: number;
    freshness: number;
  };
  isMatch: boolean;
};

export interface IMatchingEngine {
  match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult>;
}

/**
 * V1 uses only explainable phrase and keyword checks. No semantic inference
 * happens here, which keeps match decisions inspectable and testable.
 */
export class RuleBasedMatcher implements IMatchingEngine {
  async match(job: NormalizedJob, profile: CandidateProfile): Promise<MatchResult> {
    const searchable = `${job.title} ${job.description}`.toLowerCase();
    const title = job.title.toLowerCase();
    const roleHits = profile.roles.filter((role) => title.includes(role.toLowerCase()));
    const skills = [...profile.skills, ...profile.technologies];
    const skillHits = skills.filter((skill) => searchable.includes(skill.toLowerCase()));
    const excluded = profile.excludeKeywords.filter((keyword) => searchable.includes(keyword.toLowerCase()));
    const locationHit = profile.locations.some((location) => job.location.toLowerCase().includes(location.toLowerCase()));
    const workplaceHit = profile.workplacePreference === "Any" || job.workplaceType === profile.workplacePreference;
    const aiHits = profile.includeKeywords.filter((keyword) => searchable.includes(keyword.toLowerCase()));
    const breakdown = {
      role: roleHits.length ? 30 : 0,
      skills: skills.length ? Math.round((skillHits.length / skills.length) * 30) : 0,
      experience: 15,
      location: locationHit || workplaceHit ? 10 : 0,
      aiRelevance: aiHits.length ? 10 : 0,
      freshness: Math.max(0, 5 - Math.floor((Date.now() - Date.parse(job.postedDate)) / 86400000)),
    };
    const score = Object.values(breakdown).reduce((sum, value) => sum + value, 0) - (excluded.length ? 20 : 0);
    return {
      score: Math.max(0, Math.min(100, score)),
      matchedCriteria: [...roleHits, ...skillHits, ...aiHits],
      missingCriteria: skills.filter((skill) => !skillHits.includes(skill)),
      reasons: [roleHits.length ? "Title aligns with a preferred role." : "No preferred role phrase detected.", locationHit ? "Location matches a preferred location." : "Location preference was not detected."],
      breakdown,
      isMatch: score >= 70 && excluded.length === 0,
    };
  }
}