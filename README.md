# UQ Discord Bot
Discord bot for UQ First Years 2021

## Code Architecture

### Overview

Built using C#, DSharpPlus, Docker + Docker Compose

#### UqDiscordBot.Discord

Here is the main part, with a heavy emphasis on discord integration, code that relies on discord is here, anything that doesn't rely on discord, should not be here.

#### UqDiscordBot.Core

Any core models should be here, services that only rely on core models, and repository interfaces

#### UqDiscordBot.Infrastructure

Repository implementations, here is where the actual DB is chosen, ie Marten, MySQL, MongoDB, etc...
