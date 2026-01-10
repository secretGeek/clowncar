# CLOWN CAR: Things To Be Done Perhaps.

##--Done not release or advertised

----

## Have a "watch" command

it performs serving, watching for changes, triggering recompiles and deployments.

To have that you basically have to have a superset of the functionality for the "serve" command. Therefore it is easy to see you can:

## Have a "serve" command

The "serve" command takes useful parameters that assists the two core audiences.

1. people who use the serve command.

2. functions that call this command (e.g. the watch command)

cc -serve 80:80


## Build one day a responsive 404 server for everything.

It knows how to respond when it finds an entity missing.

1. It's missing -- were you hoping to create something here then?

2. It's missing -- would you like to buy one or look for one somewhere, perhaps an archive?

3. that controller is missing -- how would you like to create it? 

3. that server name is missing -- locate it on the network? apply that name to an existing server/service using DNS? Buy that domain name from the internet (or, if you are known by cc to own the domain name already then apply relevant DNS entries to it to create the domain and then look into standup *something* at the other end of it...

	- static pages
	- a virtual machine
	- an azure function or aws lambda or a web hook subscriber or anything really.


Incidentally:

"look and buy" is the best name for a shop.












## Parallel improvements

- can it be made faster through parallel chains?

- any obviously "I use sync when async is right there!" moments are resolved (low hanging async fruit.)


##


## Template language improvements

- newguid
- titlesuffix
- baseurl
-

## Template repository...

- list available templates
	...in the default template repoistory online
	...that match a string
- set the default repository location.
	- must it be online? can it be a local file location?
- able to download a template from the repository for use locally.


## markdown extensions....

- macro system
- includes
- transcludes
- spintax
- toc
- pdf

## other ideas...

- console: clowncar example or -? examples will give you examples of using it.
- able to split a file into multiple pages.
   (note i do this with glossary and tools online)
- able to have "next/previous" links
- able to turn glossary terms into links to glossary.
- Structured feedback (see what TIL ends up doing about structured feedback (edit/bug/comment/helpful))
	(this means, having things such as comments on a page, able to fav/star a page, show page specific things, log bug about a page)
- "clowncar init SITENAME [THEME-NAME]" -- which outputs:
	- creates new folder.
	- a sample theme
	- an index.md
	- a page1.md or something...
	- help (in the console) explainig how to run it
	- a ok-file for building/publishing.
	- (if the files already exist, don't overwrite them)

