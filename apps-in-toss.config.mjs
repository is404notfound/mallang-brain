import { defineConfig } from '@apps-in-toss/web-framework/config';

if (!process.env.GATE0_WEB_BUNDLE) throw new Error('Use npm run gate0:package-toss -- <WebGL directory>; stale build fallback is disabled.');

export default defineConfig({
  // Apps in Toss console app ID. Keep this fixed so a build cannot target another app by accident.
  appName: 'mallang-brain',
  brand: { primaryColor:'#27685d' },
  permissions: [],
  webBundleDir: process.env.GATE0_WEB_BUNDLE,
  navigationBar: { withBackButton:false, withHomeButton:false, withTitle:false, transparentBackground:true, theme:'dark' },
  webView: { allowsInlineMediaPlayback:true, bounces:false, pullToRefreshEnabled:false, mediaPlaybackRequiresUserAction:true },
});
