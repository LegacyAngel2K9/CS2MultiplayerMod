/**
 * Compatibility plugin for the CS2 UI webpack template.
 *
 * CSS is already emitted by MiniCssExtractPlugin. The old template referenced
 * this hook to keep the plugin list stable, but the multiplayer UI does not
 * require an additional generated presence file. Keeping a real webpack plugin
 * here avoids a hard module-load failure while leaving CSS extraction intact.
 */
class CSSPresencePlugin {
  apply() {}
}

module.exports = { CSSPresencePlugin };
