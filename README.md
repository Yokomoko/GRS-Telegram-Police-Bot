# Groestlcoin Telegram Police Bot

This bot (when supplied with a valid Telegram Bot Token) will remove media messages on a per-chat-per-user basis if the parameters have been met (1 media message per minute, currently). 3 warnings of media-spam and the user will get a 2-day media ban. The bot needs admin priviledges and also needs to not be private. 

## Building

Open the project in Visual Studio 2015 or greater, enter in your token to the parameter `BotClient`, then build (For debugging, windows or Linux) and/or publish (for click-once for windows) the project.

This repository can also be run on Linux - Simply copy the build files to the Linux machine, and install Mono:

`sudo apt-get mono-complete`

Browse to the directory you copied the build files to, and enter:

`sudo mono GifPolice.exe`
