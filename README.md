# ![Patchy](http://i.imgur.com/gbum1O6.png) Patchy: A Bittorrent Client

Patchy is an open source bittorrent client for Windows.

Patchy is powered by [MonoTorrent](https://github.com/mono/monotorrent).

**Current Status**: Beta [(Download)](http://bit.ly/patchybeta)

[![Screenshot](http://sircmpwn.github.com/Patchy/images/screenshots/1.png)](http://sircmpwn.github.com/Patchy/images/screenshots/1.png "Click to enlarge")

## Features

* Downloads torrents (from torrent files and magnet links)
* Inline file priority adjustment
* Configurable speed and connection settings (global and per-torrent)
* Seeding goals (hours seeding, target ratio)
* Torrent labels for organization
* Encrypted torrenting
* Prioritize torrents for streaming
* Seeding goals
* RSS feed support
  * Match feeds with regex and automatically download
  * Browse feeds from the application and add torrents directly

## Why should I use it?

Patchy is free and open source. Most Windows clients these days have become adware with premium versions
and other similar saddening things. Patchy will never be like this - no ads, no extra software in the
installer, and it comes with all the features, completely free.

## Download

These links will become active once the final release goes live.

* [.torrent](#)
* [HTTP](#)

## Help and Feedback

Feel free to file bug reports as [GitHub Issues](https://github.com/SirCmpwn/Patchy/issues/new). Please be
as descriptive as possible. You may also submit feature requests and ask questions through the same mechanism.

Additional resources:

* [#patchy on Freenode](http://webchat.freenode.net/?channels=patchy) - Chat room
* [/r/patchy](http://reddit.com/r/patchy) - Subreddit

## Compiling

To compile Patchy, add `C:\Windows\Microsoft.NET\Framework\v4.0.30319` to your PATH. Run `msbuild` from the
root of the repository to build. There are three different configurations. You can target a specific
configuration with `msbuild /p:Configuration=<value>`. The three configurations are:

* DEBUG: Default configuration. Builds everything with debugging symbols.
* RELEASE: Optimizes code, removes debugging symbols.
* PORTABLE: Does not build installer and related projects. Tweaks things slightly to work in a portable fasion.

## Contributing

Pull requests are welcome. Adhere to code standards already in use. Smaller, focused pull requests are more
likely to be accepted than broad, sweeping ones. Please make your pull requests in feature branches - that is
to say, create a branch for each new change you make, and it will be merged into master once deemed stable.

## License

Patchy uses the permissive [MIT license](http://www.opensource.org/licenses/mit-license.php/). In a nutshell:

* You are not restricted on usage of Patchy; commercial, private, etc, all fine.
* The developers are not liable for what you do with it.
* Patchy is provided "as is" with no warranty.

Some icons used in the Patchy UI are provided by http://pixel-mixer.com.
