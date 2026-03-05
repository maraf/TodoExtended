import type { SquadConfig } from '@bradygaster/squad';

/**
 * Squad Configuration for TodoExtended
 * 
 */
const config: SquadConfig = {
  version: '1.0.0',
  
  models: {
    defaultModel: 'claude-sonnet-4.5',
    defaultTier: 'standard',
    fallbackChains: {
      premium: ['claude-opus-4.6', 'claude-opus-4.6-fast', 'claude-opus-4.5', 'claude-sonnet-4.5'],
      standard: ['claude-sonnet-4.5', 'gpt-5.2-codex', 'claude-sonnet-4', 'gpt-5.2'],
      fast: ['claude-haiku-4.5', 'gpt-5.1-codex-mini', 'gpt-4.1', 'gpt-5-mini']
    },
    preferSameProvider: true,
    respectTierCeiling: true,
    nuclearFallback: {
      enabled: false,
      model: 'claude-haiku-4.5',
      maxRetriesBeforeNuclear: 3
    }
  },
  
  routing: {
    rules: [
      {
        workType: 'spec-analysis',
        agents: ['@architect'],
        confidence: 'high'
      },
      {
        workType: 'feature-dev',
        agents: ['@architect', '@backend', '@frontend'],
        confidence: 'high'
      },
      {
        workType: 'backend',
        agents: ['@backend'],
        confidence: 'high'
      },
      {
        workType: 'frontend',
        agents: ['@frontend'],
        confidence: 'high'
      },
      {
        workType: 'bug-fix',
        agents: ['@backend', '@frontend'],
        confidence: 'high'
      },
      {
        workType: 'testing',
        agents: ['@tester'],
        confidence: 'high'
      },
      {
        workType: 'architecture',
        agents: ['@architect'],
        confidence: 'high'
      },
      {
        workType: 'documentation',
        agents: ['@scribe'],
        confidence: 'high'
      }
    ],
    governance: {
      eagerByDefault: true,
      scribeAutoRuns: false,
      allowRecursiveSpawn: false
    }
  },
  
  casting: {
    allowlistUniverses: [
      'The Usual Suspects',
      'Breaking Bad',
      'The Wire',
      'Firefly'
    ],
    overflowStrategy: 'generic',
    universeCapacity: {}
  },
  
  platforms: {
    vscode: {
      disableModelSelection: false,
      scribeMode: 'sync'
    }
  }
};

export default config;
