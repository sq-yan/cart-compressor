# Cart Compressor — Cart Crunch

A standalone **R.E.P.O.** upgrade that makes cart hauling a personal specialization. Buy the cereal box, eat it, and compress cargo when you handle a cart.

**Public beta:** built against R.E.P.O. v0.5.1. Multiplayer synchronization is implemented but has not received a full multiplayer playtest. Please report issues with versions and reproduction steps; redact personal details from logs.

## Three personal levels

| Boxes consumed | Cargo size | Size reduction |
| --- | --- | --- |
| 0 | 100% | none |
| I | 76.67% | 23.33% |
| II | 70.83% | 29.17% |
| III | 65% | 35% |

Each box costs **at least $15,000**; vanilla purchase scaling may increase the price. Discounts are disabled for this upgrade. A fourth use is blocked without consuming the box.

- Only the player who eats the box receives the upgrade. Buying it does not upgrade the team.
- Parked carts retain compression while their owner is alive and connected.
- An unupgraded player grabbing or physically colliding with the cart cancels compression.
- Multiple qualified handlers use the lowest participating level.
- Cargo returns to normal size after leaving the cart. Mass, value and durability are unchanged.
- Only ordinary valuables are supported. Shop items, players, enemies, heads and mountable vehicles are excluded.

## Installation

Use r2modman or Thunderstore Mod Manager. Requires BepInExPack 5.4.2305 and [REPOLib 4.3.0](https://thunderstore.io/c/repo/p/Zehs/REPOLib/).

**No ShrinkCart, ScaleInCart, ScalerCore or REPOConfig required.** All players need the same version of Cart Compressor and REPOLib. Disable competing cart-scaling mods.

Manual installation: put `CartCompressor.dll` in `BepInEx/plugins/CartCompressor/`. Remove older duplicate copies first and restart the game.

## Appearance and configuration

Look for **CART COMPRESSOR**: a vanilla-shaped yellow cereal box with **CART CRUNCH** artwork, cyan cubes and a cart. Shop stock is random. The public build has no item-spawn hotkey.

Configuration: `BepInEx/config/Local.CartCompressor.cfg`. The enable switch is host-controlled. REPOConfig is optional. Old save IDs are preserved; an existing single upgrade becomes level I.

## Known limitations

Physical touch requires an actual Unity collision. The host checks carts every 0.1 seconds and sends reliable snapshots every 0.5 seconds. Late joining, host migration and packed-cart expansion need more multiplayer testing. Do not combine with other mods that resize the same valuables.

## Development

Requires .NET SDK 8+ and .NET 8 runtime for tests, plus your own game installation and REPOLib DLL. Game assemblies are not distributed.

```powershell
./build.ps1 -GameDir 'PATH/TO/REPO' -RepolibPath 'PATH/TO/REPOLib.dll'
```

The build runs 23 rule tests, checks six Harmony targets and embedded artwork, and creates a Thunderstore ZIP in `dist/`. It does not install into the game. Pure rule tests can also run without game assemblies using `dotnet test tests/CartCompressor.Tests.csproj -c Release`.

## Credits and license

Created by **sq-yan**. Concept inspired by [ShrinkCart](https://github.com/AngelcoMilk/ShrinkCart) by AngelcoMilk. This standalone implementation does not include its code or DLL. Vanilla cereal geometry is cloned at runtime; no game models are shipped.

Code and custom artwork were created with AI assistance. Artwork follows the vanilla cereal UV layout. Select **AI Generated** when listing this package on Thunderstore. Original project code is MIT licensed; game assets and REPOLib retain their respective rights.

[Source and issues](https://github.com/sq-yan/cart-compressor)

## Кратко по-русски

Съедаемое улучшение телеги, три уровня: 76,67% → 70,83% → 65% размера. Цена от 15 000. Работает персонально; игрок без улучшения снимает сжатие при захвате или физическом касании телеги. Нужна только REPOLib поверх BepInEx. Это бета, полная проверка мультиплеера ещё не выполнена.
