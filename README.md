# DiscordAndDragons
A simple discord bot designed to help D&D sessions over discord.
## After building the bot
Since the sourcode of the bot is public, you must provide your own discord bot token. The token must be provided as a runtime parameter for the bot.
## Supported features
### Dicerolling
The bot can simulate most neccessary dice rolls in D&D. The command for rolling dice is .r .roll or .dice (prefix can be changed in `Program.cs`)  
Examples:
* .r d20 - This rolls a d20
* .r 3d8 - This rolls three d8s and calculates their sum
* .r 2d6+5 - This rolls two six-sided dice, calculates their sum, then adds 5 to that
* .r d12-1 - This rolls a d12 and subtracts one from it

You can mix and match all of these examples. Dice values aren't restricted to D&D dice (you can also roll a d7 or a d666)
### Spell Data
The bot can get spell data by extracting it from the HTML code of the D&D 5E Wikidot site (now also implements caching in XML). This can be done by doing `.spell [spellname]`. This method replaced the deprecated D&D 5E API method, which lacked a significant amount of spells due to legal reasons, apparently.
### Weapon Calculation
This command allows you to calculate the hit bonus and damage bonus of weapons. This can be done by doing `.cw [dice] [dexterity modifier] [strength modifier] [misc parameters]`. Misc paramteres are:
* -f - This means that the weapon is Finesse
* -r - This means that the weapon is Thrown or Ranged
* -p - This means that the user is proficient with the weapon
* -pb:[num] - This represents the proficiency bonus of the user
### Feature Data
The bot can get class feature data by extracting it from the HTML code of the D&D 5E Wikidot site. This can be done by doing `.feature [classname] [featurename]`. Alternatively, subclass features can be acquired by doing `.feature [classname] s:[subclassname] [featurename]`. None of these implement caching as of 2020-12-18.
## Beta Features
### PDF Parser and Character Sheet Displayer
These features are still highly in development and are available in the `beta-characterhseet` branch. A PDF can be parsed by doing `.pchs` and attaching a character sheet PDF to the message. The sheet must be fully filled out. Once the parsing is finished, it can be viewed as an embed via `.dchs [character name]`. Character Name is case sensitive and won't work with excess spaces. The object is serialized to XML after parsing, which can be found in the cache folder.
