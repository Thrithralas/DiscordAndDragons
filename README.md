# DiscordAndDragons
A simple discord bot designed to help D&D sessions over discord.
## After building the bot
Since the sourcode of the bot is public, you must provide your own discord bot token. The token must be provided as a runtime parameter for the bot.
## Supported features
### Dicerolling
The bot currently supports two dice parsers. The old one supports single dice rolls with offsets of positive or negative integers. The format for dice rolls is the following: `[Dice Multiplier (Optional)]d[Dice Value][+ or - (Optional)][Offset (Optional)]` This translates to this in RegEx:
```regex
^\d*d\d+([+-]\d+)?$
```
The newer parsing model allows dice rolls and constant offsets chained by + or - operators. Additionally, dices can be applied to A prefix to be rolled with advantage, the D prefix to be rolled with disadvantage or the G suffix to be forced to use average. Each diceroll that is not a constant must abide the following RegEx:
```regex
^[AD]?\d*d\d+G?$
```
An example of this would be `A2d6+1-2d8G` where `2d6` is rolled with advantage which then the average of `2d8` is substracted from and is appended by 1.
### Spells, Spell List and Features [Under Update]
Using the `HtmlAgilityPack` library, spell and feature data can be extracted from the DnD 5E Wikidot site (http://dnd5e.wikidot.com/), as the existing D&D API doesn't support non-PHB & Beginner's Guide spells.

Spells can be accessed by using the `.spell <spellname>` command, which it formats into an embed. Spell Lists for a class can be accessed by the `.spell <class> <level = 1> <page = 1>` for classes respectively. Page and Level are optional parameters. The `.feature <class> <feature> | .feature <class> s:<sublcass> <feature> | .feature <class>:<subclass> <feature>` command is under rework as it uses the messy string-based parser. It does work mostly, allowing to get class or subclass specific features, ignoring tables.
