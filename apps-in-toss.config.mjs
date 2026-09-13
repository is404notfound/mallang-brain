import { defineConfig } from '@apps-in-toss/web-framework/config';

const webBundleDir = process.env.GATE1_WEB_BUNDLE || process.env.GATE0_WEB_BUNDLE;
if (!webBundleDir) throw new Error('Use the Gate 0 or Gate 1 package command with its successful WebGL directory; stale build fallback is disabled.');

export default defineConfig({
  // Apps in Toss console app ID. Keep this fixed so a build cannot target another app by accident.
  appName: 'mallang-brain',
  brand: { primaryColor:'#27685d' },
  permissions: [],
  webBundleDir,
  navigationBar: { withBackButton:false, withHomeButton:false, withTitle:false, transparentBackground:true, theme:'dark' },
  webView: { allowsInlineMediaPlayback:true, bounces:false, pullToRefreshEnabled:false, mediaPlaybackRequiresUserAction:true },
});
