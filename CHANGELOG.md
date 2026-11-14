# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/).

Entries up to 0.2.2 are English summaries. The full history of those releases is in the Turkish changelog, [`CHANGELOG.tr.md`](CHANGELOG.tr.md).

---

## [0.2.11](https://github.com/koyhq/koy/compare/v0.2.10...v0.2.11) (2026-11-14)


### Changed

* **calendar:** request the narrower calendar.events.readonly scope instead of calendar.readonly ([4e42317](https://github.com/koyhq/koy/commit/4e423179681fe8ffdc095849a6261924004cafab))

## [0.2.10](https://github.com/koyhq/koy/compare/v0.2.9...v0.2.10) (2026-11-14)


### Added

* **calendar:** read-only Google Calendar sync via OAuth; Outlook shows as coming soon ([557fc43](https://github.com/koyhq/koy/commit/557fc43bd9ea44abed48b63c4576bdadde03236f))
* **dock:** show tray balloon notifications as notch notification cards ([f21e58e](https://github.com/koyhq/koy/commit/f21e58e097626c51a4635e0affd7e1fcad4d6fa2))
* **dock:** pin Store (UWP) apps from the taskbar pins ([08078ad](https://github.com/koyhq/koy/commit/08078adec6a84e5cd21c2b507c99750656bf4783))
* **notch:** D layout with a separate music page and a single top bar ([e55edfd](https://github.com/koyhq/koy/commit/e55edfd8d019ca03f422f421238fde293978e6c9))
* **notch:** show the date in the collapsed notch ([3380113](https://github.com/koyhq/koy/commit/3380113ca49f68ce300e4a0b87573465765bc674))
* **platform:** support Windows 10 (version 2004 / build 19041 or later) ([21888c4](https://github.com/koyhq/koy/commit/21888c458d702fe553514bf1e45bf42ab8b0f1e5))
* **search:** jump list, special characters, spell check, dictionary and Microsoft Store sources, Firefox bookmarks and drag-out ([a1e26a8](https://github.com/koyhq/koy/commit/a1e26a8ccf517764dbc4a22a12380a2df4aa931a))
* **store:** add six theme packs and a palette-based theme generator ([a22f845](https://github.com/koyhq/koy/commit/a22f845e8b731d2655d90570e7f442d1cde08069))


### Fixed

* **calendar:** send the Google client secret and embed release OAuth clients ([12cc20e](https://github.com/koyhq/koy/commit/12cc20e51c4e055a667be3c49adc4fdfd46e5ec1))
* **uninstall:** delete calendar tokens and cache when settings are removed ([5fcdf97](https://github.com/koyhq/koy/commit/5fcdf97c6855788405da4ac757df27bc839441ca))


### Performance

* record memory measurements after feature waves ([e08bc2f](https://github.com/koyhq/koy/commit/e08bc2f5e318b48b78520b67e5e366418b0312b9))

## [0.2.9](https://github.com/koyhq/koy/compare/v0.2.8...v0.2.9) (2026-11-12)


### Added

* **extensions:** localize store summary and description by UI language ([9b60859](https://github.com/koyhq/koy/commit/9b60859e13fa2c40877c0c969e9a50000e83d889))
* **store:** add Turkish texts to the sample packages and document localization ([f6dffad](https://github.com/koyhq/koy/commit/f6dffadc78673240e53d862d5d717bad435091bd))


### Fixed

* **build:** restore shutdown order and keep advisory warnings non-fatal ([8817f55](https://github.com/koyhq/koy/commit/8817f55bc1e75c2feb3e5d75f0b5cc2ae0a923be))
* **dock:** find the jump list for packaged and Squirrel apps ([02aecfd](https://github.com/koyhq/koy/commit/02aecfd0d5debf514a45a8abddf821d34c9c57d4))
* **dock:** fix context menu z-order, outside-click dismiss, and jump list title fallback ([0a11ba5](https://github.com/koyhq/koy/commit/0a11ba58637757a39d350139d2378307a2487c76))
* **extensions:** harden the extension store (theme backup, registry, URL trust, localization) ([1d66d03](https://github.com/koyhq/koy/commit/1d66d03918359dd942648aa06046e3af599f4b29))
* **extensions:** never overwrite an unreadable or newer extension registry ([d643a03](https://github.com/koyhq/koy/commit/d643a033121ec6f092ee485f302df88625688eb5))
* **extensions:** only download packages and previews from the store repository ([4c5a81c](https://github.com/koyhq/koy/commit/4c5a81c1c37906119e56bad9f4cb93998d3d16bd))
* **extensions:** restore the original theme state and roll back half-applied changes ([2e15586](https://github.com/koyhq/koy/commit/2e155861612b72dd1b653e3265ffffba4be9c3ce))
* **store:** reject reserved device names in any dot-separated id part ([e0728d5](https://github.com/koyhq/koy/commit/e0728d56509d9b66ec785366845fa81fa776ae37))

## [0.2.8](https://github.com/koyhq/koy/compare/v0.2.7...v0.2.8) (2026-11-12)


### Added

* **appearance:** let theme packs restyle notch and dock ([9ddb99d](https://github.com/koyhq/koy/commit/9ddb99dafd8ee81d644572fc8b2591274883f06e))
* **extensions:** add store index, package installer and installed registry ([4c5c996](https://github.com/koyhq/koy/commit/4c5c99667734b3e84655bfd46f436bf384ceaeb3))
* **settings:** add developer mode toggle and redesign advanced settings page ([74b76b3](https://github.com/koyhq/koy/commit/74b76b3ffc4718b063254d4bb0bb17d9fa71872b))
* **settings:** add DeveloperMode and friendly section/property titles to AdvancedSettings ([1558550](https://github.com/koyhq/koy/commit/1558550f0783e13d7e93956e6ef45b864accb6e5))
* **settings:** add extensions page with store browsing, install and apply ([8670139](https://github.com/koyhq/koy/commit/867013903f08cdff6c2347ad42fb1ce4e44e5d8b))
* **settings:** map unit keys, remove about thanks links, and add gelismis snapshot ([65595d6](https://github.com/koyhq/koy/commit/65595d6a1046b8fa739c70635324369847a98df5))
* **store:** add extension index, sample packages and store check workflow ([d3b18c1](https://github.com/koyhq/koy/commit/d3b18c129641a8d77b784fbc393ca17108080f6a))
* **store:** paint notch and dock in the sample theme packs ([99da370](https://github.com/koyhq/koy/commit/99da370c7d6987318b4250d9801c06b58f4059ad))
* **strings:** add developer mode and advanced filter strings ([d9dd358](https://github.com/koyhq/koy/commit/d9dd35898eca4f85b451836ad07389c3ba39038a))


### Fixed

* **settings:** clean up orphan static notices in changed-only filter view ([e0c8f8f](https://github.com/koyhq/koy/commit/e0c8f8ffa40858607be0c5f19d5ff2f5007014f1))
* **settings:** fix adorner leaks, polish changed-only filter view, and redesign advanced settings ([e180826](https://github.com/koyhq/koy/commit/e18082615c6f4fabbd5ac9208636facfe7ddd920))
* **settings:** fix adorner lifecycle for changed setting indicator dots ([f12d33b](https://github.com/koyhq/koy/commit/f12d33b0ff9f3645860e09eca4685906b41e86dd))
* **settings:** preserve section headers and handle empty states when filtering changed settings ([04b61c2](https://github.com/koyhq/koy/commit/04b61c22d176351ffb0e3bcc88427bba2fef8339))

## [0.2.7](https://github.com/koyhq/koy/compare/v0.2.6...v0.2.7) (2026-11-12)


### Added

* **about:** add website button and thanks card ([9a2a8db](https://github.com/koyhq/koy/commit/9a2a8db9ec6a592e923746d2d66d809bbe436bcd))
* **appearance:** add font and UI scale settings with pure policy ([e759063](https://github.com/koyhq/koy/commit/e75906363647624b26660a95e956c1d43e7c849e))
* **appearance:** add icon pack setting and per-app icon override model ([db35753](https://github.com/koyhq/koy/commit/db357535c33775bcd97853ffc036d34ac8902324))
* **appearance:** add restricted user style parser with token allow-list ([7884ccd](https://github.com/koyhq/koy/commit/7884ccdd716bc4d613eb7e0c8e642e8bbb696e47))
* **appearance:** add style override whitelist and sanitizer ([583fe34](https://github.com/koyhq/koy/commit/583fe3421e6a23f4b8546b78cf34dcb2e9ec99ad))
* **appearance:** apply font and Koy window scale settings ([7c1cbd3](https://github.com/koyhq/koy/commit/7c1cbd3809656fa454b7faf08b0fed67bb11edf1))
* **appearance:** apply user style overrides after the theme dictionary ([92c8b34](https://github.com/koyhq/koy/commit/92c8b34d19001387b62bcee64dbdecc278b7e054))
* **appearance:** per-animation motion toggles ([f0a5707](https://github.com/koyhq/koy/commit/f0a5707a141936a95a2e98da0580c9f09d4795b7))
* **appearance:** wire per-animation motion toggles ([325dc7d](https://github.com/koyhq/koy/commit/325dc7d217261714098216b172bed857187b1a73))
* **app:** wire screen corner actions and settings rows ([b448efd](https://github.com/koyhq/koy/commit/b448efdd4569d5acdff823f39c908bca7009eba2))
* **calendar:** week view follows the first day of week setting ([f1e6315](https://github.com/koyhq/koy/commit/f1e6315fd7da5725d15642e2c15efb13196471fd))
* **clock:** add second time zone reading, week grid and wind speed formatter ([2fc4094](https://github.com/koyhq/koy/commit/2fc40943309ae38d984e92a7fcf104860d195312))
* **clock:** apply clock and date format settings ([8ecee31](https://github.com/koyhq/koy/commit/8ecee3162044c15a65e9ae09e1a7d2b5a0352d72))
* **clock:** compose peek line with optional date and second clock ([cb26d6e](https://github.com/koyhq/koy/commit/cb26d6e756cf23564da4a4ed270d60f5bcc4aff7))
* **core:** add hot corner settings, tracker and open-settings shortcut ([ec7fdfc](https://github.com/koyhq/koy/commit/ec7fdfc23f7ce2c58db9146336e9c62dcc9aa79c))
* **core:** add icon pack lookup, app icon key and pack folder setting ([60b794f](https://github.com/koyhq/koy/commit/60b794f22fb895ab81c627f114423deabf5501b0))
* **core:** resolve icons by every app key and validate pack folders ([9a3ab4c](https://github.com/koyhq/koy/commit/9a3ab4c30f000c438c08c1b6ae2944bec7b68bb7))
* **dock:** add click action policy and monitor window filter in Core ([8c396ba](https://github.com/koyhq/koy/commit/8c396ba2f043d98988359412308f7243672a9982))
* **dock:** add configurable dock item model and pure rules ([1fa7abf](https://github.com/koyhq/koy/commit/1fa7abf928cae792cbc89642d918dd305f661f75))
* **dock:** add edge reveal delay and indicator mark rules ([a563f9d](https://github.com/koyhq/koy/commit/a563f9d64d64f1c9bd5c3b101a36159eeb4905e8))
* **dock:** add grouping, monitor filter, click and label settings ([a9b275b](https://github.com/koyhq/koy/commit/a9b275bdf2277dd6f7c4e2b3496288097c2b0ace))
* **dock:** add indicator, badge and auto-hide settings with pure decision logic ([7164233](https://github.com/koyhq/koy/commit/7164233f931fe17fb491ef57065937d83b2deafc)), closes [#76](https://github.com/koyhq/koy/issues/76)
* **dock:** add layout, custom command and clock rules for dock items ([002f969](https://github.com/koyhq/koy/commit/002f969a18549a18da0ab9308fcc4814df313ad0))
* **dock:** add per-app icon change and reset in the context menu ([b0eca17](https://github.com/koyhq/koy/commit/b0eca1778ae7ff4c2804fc5eba148eee5a713a51))
* **dock:** add screen edge, length, offset, padding and icon spacing geometry ([1ee2b32](https://github.com/koyhq/koy/commit/1ee2b3292e5b41c004837278a3e89b7b2efc4561))
* **dock:** apply per-app tray icon visibility ([4b56b8b](https://github.com/koyhq/koy/commit/4b56b8b1bef275556003a2ca8e3d4fb6787bab05))
* **dock:** apply reveal delay and show hidden edge line ([d98cafd](https://github.com/koyhq/koy/commit/d98cafd7570d653f51519d59ef70d1e0eef0f715))
* **dock:** connect window grouping mode and application label settings and fix DockPage crash ([1dfda82](https://github.com/koyhq/koy/commit/1dfda82fe33b167c9969e4510dd6e8a750f10324))
* **dock:** draw and run dock items in the dock strip ([0f50677](https://github.com/koyhq/koy/commit/0f50677d2d9d656dbae5359e131048c161a328f1))
* **dock:** render indicator styles, colors and badge options ([db8d91c](https://github.com/koyhq/koy/commit/db8d91c8c214810795c54ecd0633da092d13f59c))
* **dock:** support all screen edges and geometry in dock resolver and appbar math ([d081455](https://github.com/koyhq/koy/commit/d081455f9befed27fabc2a9e058acc43df4cec9f))
* **dock:** wire edge placement, orientation and spacing in dock window ([a0668db](https://github.com/koyhq/koy/commit/a0668dbd10694f8c2f1dccbb7cfedff8f8ad4bff))
* **dock:** wire left click action and monitor filter into the dock and settings ([846ddf4](https://github.com/koyhq/koy/commit/846ddf46e9e5d76c2c427274d45f180b845739bb))
* **extensions:** add manifest model and installed list rules ([472b10c](https://github.com/koyhq/koy/commit/472b10c9c6ed5cb9a59aac64daa318f2497426d7))
* **i18n:** add icon pack and per-app icon strings ([5ab9ecb](https://github.com/koyhq/koy/commit/5ab9ecb5ac8d030afa2240b3ae52b83f0a2abfd4))
* **input:** add action enum, binding model and hot corner math ([b25ab29](https://github.com/koyhq/koy/commit/b25ab29c0e0756129bff404965f03ca2715c7db4))
* **notch:** add background source model and animation policy ([a13a816](https://github.com/koyhq/koy/commit/a13a816c6994c67b2c1f93da73af17ec90930f67))
* **notch:** add live background policy, settings and file validation ([4c24807](https://github.com/koyhq/koy/commit/4c24807276f48bc712c44d709762e98512e4f837))
* **notch:** add open and close delays, drag-to-open, tab order and tab bar style ([949d284](https://github.com/koyhq/koy/commit/949d284ecee5b3e97b07bc3d9377dc8e5fd7718a))
* **notch:** add tab bar, opening, home card and visualizer settings ([91c1e5b](https://github.com/koyhq/koy/commit/91c1e5bbbb7ac8b3ee12d53428d201b46f850df6))
* **notch:** cap decoded size of live background sources ([b6d0c2b](https://github.com/koyhq/koy/commit/b6d0c2bddf989b2b570282ce5d08e91225b84194))
* **notch:** connect HomeCards and audio visualizer customization settings ([f662ff4](https://github.com/koyhq/koy/commit/f662ff492e1f61e99a1dec9c23dd75891f8cf21a))
* **notch:** render live background under notch and music card ([596afd6](https://github.com/koyhq/koy/commit/596afd6e2a075fab9c4a79f87ddc98ec59ef6bfc))
* **notch:** settle without overshoot on close and bounce on shelf drop ([aa7229c](https://github.com/koyhq/koy/commit/aa7229c999bf9f02eaa1f65583e7e1e4751ab33c))
* **rules:** add context profile switcher, rule store and list editor ([81a6c15](https://github.com/koyhq/koy/commit/81a6c156b542b13ae63320457ccf22a7d0bcfd77))
* **rules:** add rule model, validator and evaluator in Core ([b8e8f3a](https://github.com/koyhq/koy/commit/b8e8f3a746a0046205cc06e9a9085b6799d489bf))
* **rules:** switch profiles from the foreground app event ([166bc21](https://github.com/koyhq/koy/commit/166bc212b0b9a880285c42050f2ed1a7210a00b4))
* **search:** add unit and color converter sources ([0853575](https://github.com/koyhq/koy/commit/08535755a168091f29a511dd38a60643d1cd2e10))
* **search:** register converters and add strings ([35e8168](https://github.com/koyhq/koy/commit/35e81683432e7ab62a0db1605388f50bab8f5d0a))
* **settings:** add apply/revert, advanced page and live reload of settings.json ([6719c4d](https://github.com/koyhq/koy/commit/6719c4d061eeaf561e91195fa5eed42bd9286491))
* **settings:** add clock, unit and tray icon visibility settings in Core ([69b17b7](https://github.com/koyhq/koy/commit/69b17b76e80f1926d89ba5a81e41371a881034db))
* **settings:** add dock edge, length, offset, padding and spacing controls ([cdb2cc9](https://github.com/koyhq/koy/commit/cdb2cc9805728a3aeb1a008137417dbca21bfdac))
* **settings:** add dock indicator, badge and auto-hide rows ([95f9e0a](https://github.com/koyhq/koy/commit/95f9e0aea5395cb6fb1d36bd13e2ba296ef7071b))
* **settings:** add icon pack row to the Appearance page ([c5e4e96](https://github.com/koyhq/koy/commit/c5e4e96df0bc61a79774d41ad23ff39fb22efdc9))
* **settings:** add profile codec with validated import and section apply ([c6bfc05](https://github.com/koyhq/koy/commit/c6bfc05d1ad5db8e39bc198c89d57f5a441e1d7c))
* **settings:** add profile service to list and apply profiles ([d04f2c8](https://github.com/koyhq/koy/commit/d04f2c8e32b40d2f7260cbd1481d7b8afc556217))
* **settings:** add profiles section to the General page ([9fafcd3](https://github.com/koyhq/koy/commit/9fafcd3e5fd335c268c597b72cbc30cd35f99223))
* **settings:** add pure search, changed-from-default and apply/revert timeout policies ([76c96c5](https://github.com/koyhq/koy/commit/76c96c53086d4b92644d0b3aede7dcec49708c6d))
* **settings:** add rules section to the General page ([6becdfe](https://github.com/koyhq/koy/commit/6becdfe96c8e02d52e881063a78b78f1ad209ba2))
* **settings:** add saved profile library and store ([25092df](https://github.com/koyhq/koy/commit/25092dfb032e1100b3563949f7be481636f71834))
* **settings:** add settings search, changed-only view and per-page reset ([f99bf98](https://github.com/koyhq/koy/commit/f99bf98f6d843eb9ca1dfb327a0de2921b0957a4))
* **settings:** add user style file section to the Appearance page ([9074417](https://github.com/koyhq/koy/commit/90744171cf94e24abfcfed53266827143b1276ab))
* **settings:** add UserStyleOverridesEnabled appearance setting ([0575c25](https://github.com/koyhq/koy/commit/0575c250a8387faa33195c735268d3a711dc8d95))
* **settings:** add visuals section for live background ([bb3ba56](https://github.com/koyhq/koy/commit/bb3ba56129fdbc482c978850b21e1ba3382afaf4))
* **settings:** apply temperature and network speed units ([ad52d73](https://github.com/koyhq/koy/commit/ad52d73ca61110cd4d3c09788228b27efb3e99b2))
* **settings:** second time zone, show date and wind speed unit ([c0f1301](https://github.com/koyhq/koy/commit/c0f130172eb4d2bb6a5e1cf22c10b0822a451aa3))
* **settings:** wire home cards, visualizer, and dock grouping settings in KoyWindow ([e43fc25](https://github.com/koyhq/koy/commit/e43fc25643ba1c3829e0fb13c97d1b5f066c032d))
* unified integration of N1-N19 customization suite ([e72d70d](https://github.com/koyhq/koy/commit/e72d70d20c1a7bfe6cd83bfe302befbf2e7167c7))


### Fixed

* **appearance:** add KoyNotchFont and KoyClockFont to AllowedTokens ([7570780](https://github.com/koyhq/koy/commit/75707802d413f2f8057504308915e1862f2e1ff1))
* **appearance:** cap font family length and guard non-finite DIP scaling ([1c5d67f](https://github.com/koyhq/koy/commit/1c5d67f9869675116505a9d6060dc26724f2bf20))
* **appearance:** freeze style overrides and cap sanitizer report size ([3973380](https://github.com/koyhq/koy/commit/3973380829e99b22a27e73f6651646368a1d1099))
* **appearance:** keep the Koy window size when the scale did not change ([a5c2967](https://github.com/koyhq/koy/commit/a5c29674fa51369961a0b9e5110064c0b792c9b9))
* **appearance:** make style whitelist immutable and cap echoed names ([c0e9360](https://github.com/koyhq/koy/commit/c0e93605e122b12250e01f7898a321f20db9e4cb))
* **appearance:** reject device names and dot/space-terminated path parts in icon paths ([bba5dab](https://github.com/koyhq/koy/commit/bba5dab4ad9561f5eea525503a6f03fe32eabc2f))
* **appearance:** reject UNC, ADS and dot-segment icon paths ([ac52453](https://github.com/koyhq/koy/commit/ac524530b1e5034e74d54b502afac4a68b541d1d))
* **appearance:** treat a NaN motion scale as no animation ([662c455](https://github.com/koyhq/koy/commit/662c455ba70d1f0b14203a71e7c832bebc0b2cdb))
* **background:** reject alternate streams, device names and non-canonical paths ([c4f8866](https://github.com/koyhq/koy/commit/c4f88663e3d551fd1d93db7309c17d1644ad7cd8))
* **background:** stop animation when hidden or motion is reduced, harden path checks ([c36fd42](https://github.com/koyhq/koy/commit/c36fd426600d16d01a1e58cfc006d94d47b4a851))
* **clock:** assign year-end days to the next year's first week ([2ffba43](https://github.com/koyhq/koy/commit/2ffba43d0702223b688684bc36d501bce72c1479))
* **dock:** default reveal to the current edge hold and never strand a hidden dock ([5e2b329](https://github.com/koyhq/koy/commit/5e2b329ddfe7344f409d0da72ca7185f55eeca23))
* **dock:** harden dock item loading and add pinned order migration ([fef3ebd](https://github.com/koyhq/koy/commit/fef3ebd0bc50600a859aefc0ad7b8df0864d18f7))
* **dock:** harden edge layout against undefined edges and degenerate geometry ([0fc4ab8](https://github.com/koyhq/koy/commit/0fc4ab8b5e53de6c70a717440e76e34cd8d2b51a))
* **dock:** keep edge thickness and offset inside the work area ([a13519c](https://github.com/koyhq/koy/commit/a13519c747f2ddcb9112080360cda36886508f0e))
* **dock:** let unsaved tray app rows follow the new icon default ([37c5fc2](https://github.com/koyhq/koy/commit/37c5fc240f3ab77c4372a2fe766613f80ddc5e1f))
* **dock:** support vertical layouts, folder popups, dodge handles and appbar edge release ([a6465a6](https://github.com/koyhq/koy/commit/a6465a6a074c11a6bf93839788cf0c73db4abf7f))
* **dock:** treat undefined grouping modes as no grouping ([66536d2](https://github.com/koyhq/koy/commit/66536d26f5b3a45de17af6f4a6cce19d8d1412b2))
* **extensions:** harden manifest text, ids and read-only lists ([154e6af](https://github.com/koyhq/koy/commit/154e6af2fab05492d522023cd16b818d1f51045b))
* **extensions:** reject untrimmed text, bound capability reads, seal manifest construction ([53a90bf](https://github.com/koyhq/koy/commit/53a90bfb802f02b75e64d9b05a64d485a281ba82))
* **input:** check built-in shortcut conflicts, cap lists and ignore shared monitor corners ([6c1fb96](https://github.com/koyhq/koy/commit/6c1fb9695e372b3c3b1388b94f93975d37bb3837))
* **input:** reserve custom action id only after the record is valid ([becc3e5](https://github.com/koyhq/koy/commit/becc3e5806e6ce01e75eb802e3107f885a0ef3c4))
* **input:** track screen corners with a mouse hook instead of polling ([a3c36b1](https://github.com/koyhq/koy/commit/a3c36b169e857a419076d9965953fbb09721503c))
* **notch,dock:** restore 24-band music spectrum, dynamic home card order, and default grouping to Always ([78ffaf6](https://github.com/koyhq/koy/commit/78ffaf679aef6b21d846188a61ac015001bb67cc))
* **notch:** keep today's close-on-leave and drag-to-shelf in every open mode ([add0416](https://github.com/koyhq/koy/commit/add0416babab4388ed8704c73cfbde42d0a8e71f))
* **rules:** bound rule input sizes and harden evaluator edge cases ([42a0e33](https://github.com/koyhq/koy/commit/42a0e33722d5a896a7a3f7bf2cbab94f13c0ef91))
* **rules:** drop whole rule when any condition is invalid ([b9220cf](https://github.com/koyhq/koy/commit/b9220cf4915807e3d137041bd99a5b30c9f555c0))
* **settings:** cap dock items at 32 on load ([0ebbdd6](https://github.com/koyhq/koy/commit/0ebbdd6130fddbd3bfaa9770e66f98c89c65246f))
* **settings:** clamp remaining apply/revert time to the timeout ([a791581](https://github.com/koyhq/koy/commit/a791581a3f4ba56a178ad395d05c4b1b5df3f80b))
* **settings:** drop only the broken list entry instead of the whole section ([e4bd861](https://github.com/koyhq/koy/commit/e4bd86165c6f3c28c556e066f0da644db020e0f6))
* **settings:** drop only the invalid leaf field on deep JSON paths ([a70a5d3](https://github.com/koyhq/koy/commit/a70a5d3dce2153f9d7e4b496454ba36d37dca606))
* **settings:** ignore app-owned state in changed-from-default and harden policies ([bf2041e](https://github.com/koyhq/koy/commit/bf2041e309ad866d52362af29bd6cb631763df22))
* **settings:** keep local app icon override paths out of exported profiles ([64c69b8](https://github.com/koyhq/koy/commit/64c69b86bead09e61c970fc273b4aa4699936756))
* **settings:** keep the local start icon path out of exported profiles ([89b8a86](https://github.com/koyhq/koy/commit/89b8a869c78e93dc47501b00064abadb781e69cb))
* **settings:** reject undefined enums, infinite numbers and unsafe values in imported profiles ([9f8176a](https://github.com/koyhq/koy/commit/9f8176a8af43d32bcadf00562b4eb9dd2c1d7ffa))
* **settings:** reset only the invalid field instead of the whole file ([c7151ad](https://github.com/koyhq/koy/commit/c7151ad1a889c84db3bfb0ffb928121c88213a9f))
* **settings:** validate fields added by the stacked settings PRs in profile import ([cf17e64](https://github.com/koyhq/koy/commit/cf17e6490185e61a4e76e37d3e06ba1e7d7a5d58))
* **settings:** walk JSON paths through dotted dictionary keys ([8b085de](https://github.com/koyhq/koy/commit/8b085def5931ae3c0d0a43fea6e84f7f34c21651))
* **site:** version asset URLs so updated images are not served from cache ([ef6ac9e](https://github.com/koyhq/koy/commit/ef6ac9e3f383ea808c69d1074a3daaf5038bb343))


### Performance

* **notch:** align clock refresh to the minute ([ae85d0a](https://github.com/koyhq/koy/commit/ae85d0a71b69b9e906a143967bd563cf843866ab))

## [0.2.6](https://github.com/koyhq/koy/compare/v0.2.5...v0.2.6) (2026-11-11)
Highlights:
- **Glass:** Appearance › Material is now Solid, Frosted or Glass. The Koy window, the tray menu and the setup wizard use the Windows acrylic blur; the open notch and the dock become slightly see-through. On a bright wallpaper Koy steps the level up on its own so text stays readable.
- **Notifications on the notch:** Windows notifications show briefly as cards on the notch (for example a coding agent saying it is done). You can hide the message text, and nothing shows during Focus or presentations.
- **Update card:** when a new version is out, the notch shows it once with an Update button.
- **Floating island and ring battery:** the notch can float just below the top edge as a capsule, and a new idle display shows the battery as a ring with the day and time.
- **Album colors:** the music card takes its color from the album cover, or uses the cover itself as the background. Plain cards are now neutral grey.
- **Dictation tab:** voice command settings have their own tab. Choose Claude's permission (read-only by default, Edits or Auto) and its model; the full reply opens in a card under the notch.
- **Dock:** middle-click a window preview to close that window.



### Added

* **appearance:** add frosted and glass material levels ([e4aad69](https://github.com/koyhq/koy/commit/e4aad69f5e398d1d2383005a5945482a570dea87))
* **appearance:** give the tray menu and setup wizard the system acrylic frame ([f1705f1](https://github.com/koyhq/koy/commit/f1705f19cbb6e22ce470123a4309ddfcf7630d27))
* **appearance:** use the system acrylic backdrop for the Koy window ([75b3ac9](https://github.com/koyhq/koy/commit/75b3ac940ff538aeb43c5b3d37424c556b622f04))
* **media:** add the music card style setting and a cover fingerprint ([857e99a](https://github.com/koyhq/koy/commit/857e99a1c01a6fc6d04a239d182e4ae3ad47bb5d))
* **media:** extract an accent palette from album artwork ([fc2c492](https://github.com/koyhq/koy/commit/fc2c4921cf532f94e70ac24feb78dfb3d1763a9a))
* **music:** follow the glass material on the music card ([333a801](https://github.com/koyhq/koy/commit/333a801a08bc3e87dbb9ad265e4dbd985b2ecfe0))
* **music:** tint the music card and use the album artwork as its backdrop ([e7b9f6f](https://github.com/koyhq/koy/commit/e7b9f6faf0e04a0b0200c879b79068fa941a8ad2))
* **music:** use neutral tones for the plain music card ([f0f44a1](https://github.com/koyhq/koy/commit/f0f44a1e8805652d1f2f03052166f2758e5344e8))
* **notch:** add a floating island style ([f528d5a](https://github.com/koyhq/koy/commit/f528d5a7bdeba14c6afd06602afdcd28db78b02f))
* **notch:** add a ring battery and day-time idle display ([70ab63e](https://github.com/koyhq/koy/commit/70ab63e0670855b3855bf655fe2ede19802da027))
* **notch:** show an update card on the notch when a new release is available ([887b95a](https://github.com/koyhq/koy/commit/887b95ab1d4f01280ec0029ef57de8320092e4a7))
* **notch:** show the Do Not Disturb state on its control tile ([611af88](https://github.com/koyhq/koy/commit/611af885f3933ef2cad9192941236063a8beada5))
* **notch:** show Windows notifications as cards on the notch ([09f3217](https://github.com/koyhq/koy/commit/09f32178726af9206de60a810de04352699942df))
* **notifications:** add notification diff, poller, queue and card presentation ([62d5455](https://github.com/koyhq/koy/commit/62d545545fabc72f2db76f187fdc8bd46777f854))
* **notifications:** poll the Windows notification list without package identity ([08bbe41](https://github.com/koyhq/koy/commit/08bbe4188ed78752e662fc926d553dfc49a2b1e1))
* **voice:** add Dictation tab with Claude permission mode, model choice and reply card ([dee5f8b](https://github.com/koyhq/koy/commit/dee5f8be3a1c9fda2b6c58778f85fa355e304d48))


### Fixed

* **dock:** close the window when its preview is middle-clicked ([66d4bc0](https://github.com/koyhq/koy/commit/66d4bc05146b50981cca15e1bccea8308f00f0b5))
* **notch:** crop transparent margins so notification logos fill the icon circle ([b243c5d](https://github.com/koyhq/koy/commit/b243c5d6e12104d701de094d17e9f51617f74986))
* **notch:** keep the notch visible while a card is shown ([2ca6ea1](https://github.com/koyhq/koy/commit/2ca6ea12909a830b4c8bc5c71a3a287270d2ad6e))
* **notch:** show the right app icon on notification cards ([6c72ad6](https://github.com/koyhq/koy/commit/6c72ad61c1cd0e5e6b1831a8fd77e48fc64afaa9))
* **notifications:** detect Do Not Disturb and focus sessions on Windows 11 ([123f4c0](https://github.com/koyhq/koy/commit/123f4c045de48894d03a690efdad5132ffd06b77))
* **notifications:** drop the unreliable registry check for Do Not Disturb ([b0fdc4e](https://github.com/koyhq/koy/commit/b0fdc4ea8fbe229831fa791891aacde50dc8f083))
* **notifications:** treat a replaced notification with the same id as new ([2cae303](https://github.com/koyhq/koy/commit/2cae303041fcc857b71bad474226a7e6349c4279))
* **voice:** default to read-only permission, localize model list and invalid model error ([980eb09](https://github.com/koyhq/koy/commit/980eb098c113e163642b44b585cc4ab7fc9d6cde))

## [0.2.5](https://github.com/koyhq/koy/compare/v0.2.4...v0.2.5) (2026-11-09)
Highlights:
- **Voice command in Turkish and English:** Ctrl+Alt+V now opens a box in the notch and starts Windows voice typing (Win+H), which writes in your keyboard language, so Turkish works without installing a speech language. You can also type the command. Enter runs it, Esc cancels, and the first line of the reply shows in the notch; click it for the full reply.
- **Web search in your browser:** when nothing matches, the search opens your default browser and searches with its own engine (Chrome, Edge, Brave, Vivaldi, Firefox and others) instead of DuckDuckGo.
- **Clearer errors:** if the coding tool fails, the notch shows its own message, for example "Not logged in", instead of just an exit code.



### Fixed

* **notch:** show the voice command reply and keep Enter working ([afacb55](https://github.com/koyhq/koy/commit/afacb55aea7af8c125590ffa73a4fdafbc10f376))
* **notch:** use Windows voice typing so voice commands work in Turkish and English ([3078a72](https://github.com/koyhq/koy/commit/3078a72a44f8f4d9c33ecccba81b3a9b25f1df41))
* **search:** search the web in the default browser with its own engine ([7063934](https://github.com/koyhq/koy/commit/70639341f0ec69000e4b4cf7e6cf71d3ec52690d))

## [0.2.4](https://github.com/koyhq/koy/compare/v0.2.3...v0.2.4) (2026-11-09)

Highlights:
- **Voice command in the notch:** press Ctrl+Alt+V, speak, check the text in the notch and press Enter to run Claude Code, Codex, Antigravity or your own command. No shell is used, and Esc cancels. Dictation uses Windows online speech recognition, so audio goes to Microsoft only while Koy is listening.
- **Song lyrics:** the Home music card shows synced lyrics for the playing track, and clicking a line jumps there. Lyrics are off by default; turn them on with the small button on the music card (the track title is then looked up on LRCLIB, and .lrc files in Music\Lyrics are used first).
- **Bluetooth battery:** BLE keyboards, mice and headsets that Windows shows without a battery level now show one, without jumps while typing or when lights change.
- **Start button:** pick the Start icon on the dock, and right-click it for a Koy-styled Win+X menu.
- **Shortcuts:** any key can be a shortcut and can replace a Windows shortcut. The notch now opens on hover by default.


### Added

* **bluetooth:** show battery for BLE devices that Windows reports without it ([44e731d](https://github.com/koyhq/koy/commit/44e731d174d61c1984783f982413386d6f2bc7f0)), closes [#50](https://github.com/koyhq/koy/issues/50)
* **dock:** add a Koy-styled Start right-click menu ([2519c4e](https://github.com/koyhq/koy/commit/2519c4e5e3bf4da9ff2585a954ba4dd66937b27d))
* **dock:** let users choose the Start button icon ([7f80337](https://github.com/koyhq/koy/commit/7f8033706282ff45702310cf6d1b93c04bd9887f))
* **notch:** add synced lyrics to the large music card ([6f3f7a2](https://github.com/koyhq/koy/commit/6f3f7a25e862d85c6a83009c65003572a30552ff))
* **notch:** open the notch on hover by default ([d564720](https://github.com/koyhq/koy/commit/d564720143cb8aeda136ec759596dc232e445ed6))
* **notch:** run a coding assistant from a voice command ([aa91b68](https://github.com/koyhq/koy/commit/aa91b6813a166b3a811ad2be2f36ad73f6e6dcfd)), closes [#48](https://github.com/koyhq/koy/issues/48)
* **notch:** show lyrics directly on the Home music card ([5c85e05](https://github.com/koyhq/koy/commit/5c85e054e7b9eaddf008cdc7f1f9231d74bda37b))
* **notch:** turn lyrics off by default with a button on the music card ([9f8b123](https://github.com/koyhq/koy/commit/9f8b12383f0531e707c37f0363c3c11632848f06))
* **shortcuts:** allow any key and override Windows shortcuts ([a07507c](https://github.com/koyhq/koy/commit/a07507c353b6101cbe28105e8fa47e7413c4da76)), closes [#47](https://github.com/koyhq/koy/issues/47)


### Fixed

* **dock:** refresh the custom icon label when the language changes ([4ac48f8](https://github.com/koyhq/koy/commit/4ac48f81d65f874c884ab40bdeb6e4d258a6125c))

## [0.2.3](https://github.com/koyhq/koy/compare/v0.2.2...v0.2.3) (2026-11-08)

Highlights:
- **Search in the notch:** press Win+Shift+Q to search apps, open windows, files, settings, bookmarks and the web, do quick math, or close a running app, all from the notch. Alt+Shift+F2 opens it with the clipboard text.
- **English everywhere:** Koy now speaks English as well as Turkish and picks the Windows language on first run. "What's New" follows the app language, and the website and repository open in English.
- **Dock icon alignment:** on the full-width dock, app icons can sit on the left, in the center or on the right.
- **Safer search:** text from the clipboard or with several lines asks before it runs as a command, closing an app is the default and force-quitting asks first, and search history is off unless you turn it on.


### Added

* **notch:** add search panel inside the notch ([#40](https://github.com/koyhq/koy/issues/40)) ([14c3939](https://github.com/koyhq/koy/commit/14c39392fa76530b177ee2d936c9d5efa9d450ac))
* **i18n:** complete English support and follow the system language on first run ([#46](https://github.com/koyhq/koy/issues/46)) ([58e794a](https://github.com/koyhq/koy/commit/58e794a625c2e8b087b696dba274813879f32cb2)), closes [#37](https://github.com/koyhq/koy/issues/37)
* **dock:** add icon alignment option for the full-width dock ([#41](https://github.com/koyhq/koy/issues/41)) ([f4942df](https://github.com/koyhq/koy/commit/f4942df28ec6d3c7a53139edcaab7c943fe4a8c8))
* **site:** add English and a language switch, default to English ([#45](https://github.com/koyhq/koy/issues/45)) ([276acfe](https://github.com/koyhq/koy/commit/276acfe7e904115c96a528679d30c33610a0f21e)), closes [#35](https://github.com/koyhq/koy/issues/35)


### Fixed

* **search:** ask before running clipboard or multi-line text as a command ([bc47c75](https://github.com/koyhq/koy/commit/bc47c751b1f56631fbf52bbb46d888744d139d9f))
* **search:** make graceful close the default process action and verify process identity ([233cbb3](https://github.com/koyhq/koy/commit/233cbb3f68278afadf80d9c0a14369124845bbe2))
* **search:** turn history off by default and never store multi-line or clipboard text ([c494301](https://github.com/koyhq/koy/commit/c4943018693dbeff3623d54c1ba92f172995f953))
* **search:** guard file search state with a lock and dispose its cancellation sources ([d71a820](https://github.com/koyhq/koy/commit/d71a820d409e4ffdf30fc408ef7e3a7eec176ba9))
* **i18n:** refresh computed settings text when the language changes ([5759104](https://github.com/koyhq/koy/commit/5759104d7f4b9c4ff17a8930d4b5aac53a9d1a35))


### Performance

* **search:** cache PATH lookups per panel session and skip network directories ([4d201e6](https://github.com/koyhq/koy/commit/4d201e6af8ec4b3a9d053d27f57d237a5e794337))
* **search:** reuse parsed bookmarks until the file changes ([728b2e3](https://github.com/koyhq/koy/commit/728b2e300f2cf8574a020c8efbb32c57d8d7384d))

## [0.2.2](https://github.com/koyhq/koy/compare/v0.2.1...v0.2.2) (2026-11-08)

Highlights:
- **Easier-to-read release notes:** "What's New" and the update window no longer show links, commit hashes and technical labels, and long lines are no longer cut off.

## [0.2.1](https://github.com/koyhq/koy/compare/v0.2.0...v0.2.1) (2026-11-08)

Highlights:
- **Jump list:** Right-clicking an app on the dock shows recent files, frequent items and the app's own tasks, as in Windows.
- **Always floating:** The floating capsule stays in the air even when a window comes close or is maximized.
- **Privacy dots:** A green dot in the notch while the camera is in use and an orange dot for the microphone; hover to see which app is using it.
- **New update flow:** Updates no longer download on their own. Koy asks first, shows what changed and displays download progress.
- **More stable dock:** With multiple displays, "Dock down" no longer lifts when focus moves to the other display, and Koy no longer changes the taskbar auto-hide setting when the dock is off.

## [0.2.0](https://github.com/koyhq/koy/compare/v0.1.1...v0.2.0) (2026-11-07)

Highlights:
- **Dock folders:** Drag a pinned app onto another to make a folder, shown on the dock with a 2×2 preview.
- **Windows-style full-width dock:** 48 px, attached to the screen edge, with "Always show" or "Auto hide".
- **Compact tray, press feel and accent color:** The tray is a narrow arrow next to the Koy button, icons dip when pressed, and your accent color is used on the dock and notch.
- **Notifications in the notch:** Charger plugged in or removed, low and full battery, and Bluetooth headphones connecting with their name and battery.
- **More stable dock:** Maximized windows no longer resize on their own, and changing a setting no longer restarts the dock.
- **Repository moved:** Koy now lives at [github.com/koyhq/koy](https://github.com/koyhq/koy). The old address redirects.

## [0.1.1](https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1) (2026-11-06)

Highlights:
- New notch design with 7 tabs (Home, Focus, Calendar, Shelf, Clipboard, System, Control).
- Wi-Fi and Bluetooth lists, as in Windows.
- Bars that move with the music and an expandable music card.
- A watchdog process that brings the taskbar back in every case, and single-instance protection.
- Multiple displays, shortcuts, dark and light themes, and an uninstall screen.
- Settings that were display-only in 0.1.0 now really work.

> **Upgrading from v0.1.0:** v0.1.0 had no automatic update check. Install this release with Settings › General › "Check now" or with the new installer; later releases arrive automatically.

## [0.1.0] - 2026-11-04

Highlights:
- First public release of Koy: a spring-animated notch and a dock for Windows 11 in a single process, with no browser engine.
- Notch panels for music, calendar and Pomodoro, shelf, control center and status, plus volume and brightness indicators.
- A floating capsule or full-width dock with window proximity modes and live window previews.
- A separate notch and dock on each display, an 8-page settings window, a setup wizard, and English and Turkish.

[0.1.1]: https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/koyhq/koy/releases/tag/v0.1.0
