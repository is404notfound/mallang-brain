import { defineConfig } from '@apps-in-toss/web-framework/config';

if (!process.env.GATE0_WEB_BUNDLE) throw new Error('Use npm run gate0:package-toss -- <WebGL directory>; stale build fallback is disabled.');

export default defineConfig({
  // Local packaging identifier only. Confirm the console appName before device testing.
  appName: process.env.TOSS_APP_NAME || 'mallang-brain',
  brand: { primaryColor:'#27685d' },
  permissions: [],
  webBundleDir: process.env.GATE0_WEB_BUNDLE,
  navigationBar: { withBackButton:true, withHomeButton:true, withTitle:false },
  webView: { allowsInlineMediaPlayback:true, bounces:false, pullToRefreshEnabled:false, mediaPlaybackRequiresUserAction:true },
});
