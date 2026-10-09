with this "modloader" you can launch and play tabz with mods (: . i say "modloader" in quotes because currently there is no way to have more than one mod active at a time
launch TAHZ Run This.exe
from here, you can change your name, and select other mods.

this download include PandaMod by default, so here is some info about it

PandaMod tabz mod allows play without steam, and will also join steam rooms (if they exist)

the mod sadly doesnt work without internet as far as i can tell

add: a minimap, more colors, and console commands

to customize the minimap, you can change the map.png file in the same folder as the GAME.exe. its recommended to use a blank map as template. you can generate a blank map by deleting map.png and entering a game (actual game, not main menu)

you can change where the map appears on screen with coords.txt.  coords.txt has 2 lines with numbers, representing x and y. (0,0) is bottom left, (1,1) it top right. note that 0,0 and 1,1 will be a bit off screen , so i reccomend 0.12,0.12 or 0.88,0.88.  the default is 0.12,0.88. (top left). if you messed coords.txt and map wont load, you can delete coords.txt and enter a game and it will regenerate as the default. putting your map in the center of the screen(0.5,0.5) isnt recommended as it will cover the pause menu.

you can change the size of the map with size.txt . size.txt is identical in every way to coords.txt, but with size. 0,0 = non existant map. 1,1 = map that is the size of your screen. defualt is 0.25,0.25.  note that if you change the size of the map, you probab;y will have to change the values in coords.txt, so play around with it for a bit

you can change your name by changing name.txt or with TAHZ.exe

List of commands. all commands start with / 
you can execute commands from the chat in game. while in game, press enter to open the chat/console, enter again to chat/execute command
minimap = toggles minimap
grass = toggles grass, if command doesnt work the first time, try it again before reproting it broken
giant = spawns giant near tall town //somtimes doesnt work ):
'bluron' and/or 'bluroff' to turn blur on and off
'fogon' and 'fogoff' to turn fog on and off

turning fog off messes with lightning, so if its too hard to see, turn it back on. since at nighttime in game having no fog is practicallt impossible to see, when fog is off light levels will be the same as they are during the day. even with this fix, its sometimes shadaows and objects are just very hard to see without fog being on. frankly what im trying to say is that the fogoff command makes the game look bad

