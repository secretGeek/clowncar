# Clowncar: .net core static website builder

🚗🤡🤡🤡🤡🤡🤡🤡 *infinite clowns emerge from a tiny car!*


Clowncar generates a static html website, from a folder full of markdown files, using very simple templates.

Clowncar is a .net core console app. It can be compiled for Windows or Linux.

(Currently it is only tested on Windows, and I know there will be a few bugs on Linux, related to case-sensitivity and slashes versus back slashes. Linux isn't my priority at the moment, but pull-requests are very welcome!)

The markdown conversion is provided by [markdig](https://github.com/lunet-io/markdig) which is based on commonmark and has a lot of [interesting extensions](https://github.com/lunet-io/markdig#features).



## Help -- usage

	> .\clowncar.exe -?
	
	clowncar version 0.0.1
	Turn markdown to html.

	Usage: clowncar [options]

	Options:
		-m, --rawmarkdown=VALUE    the raw markdown
		-i, --file=VALUE           input file name
		-p, --path=VALUE           path
		-r, --recurse              recurse
		-t, --template=VALUE       default template
		-n, --notemplate           no template!
		-d, --dryrun               dry run
		-o, --output=VALUE         output path
		-?, -h, --help             this message


## Examples:


### Simplest possible example

You can use it to just convert raw markdown into html:

    .\clowncar.exe --rawmarkdown="# Hello world!"

Result:

    <h1 id="hello-world">Hello world!</h1>


And you can use the literal string `\n` to indicate new lines, so that a "one-liner" can have multiple lines.

    .\clowncar.exe --rawmarkdown="# Hello world!\n\n**new paragraph**"

Result:

    <h1 id="hello-world">Hello world!</h1>
    <p><strong>new paragraph</strong></p>

### Convert a single file (with default template or none)

    .\clowncar.exe --file="example.md"

Assuming there is a file called `example.md` in the local folder, there will now also be a file called `example.html` that uses the in-built [default template](default-template.md)

And you will see output in the console such as:

    ~~> .\example.html 508 chars, defaultTemplate

To force it to use no template at all (just save the converted markdown) use the `--notemplate` flag

    .\clowncar.exe --file="example.md" --nottemplate

### A Word about Templates....

Before showing examples with templates, I must say a word about templates: **Basic**. They are *really* basic, as you will see.

By long-standing tradition, template files used by clowncar have the `.clowntent` extension. The reasons for this are lost to history. You don't need to follow the convention.

A template is just a plain `html` file, but with these two special tokens embedded somewhere inside, which will be replaced at go time:

 * `{{body}}` &mdash; Put this token where you want the generated HTML to go.
 * `{{title}}` &mdash; The title of the document, which will be based on the name of the markdown file (with underscores replaced by spaces)

There are no other fancy features or capabilities in the template language. It's not even a language, just a literal file with two tokens that get replaced. That's it.

### Example of using a template

You can specify a template from the command line:

    .\clowncar.exe --file="example.md" --template="template.clowntent"

Now, instead of using the built-in default template, it will use the template file you have specified.


### Example of building an entire site with your own template

All of the above is just pre-amble. Here is how you are really intended to use clowncar -- to build an entire site.

Some suggestions:

* You should specify an `--output` path, so that the results ends up somewhere other than intermingled with the markdown. 
* You should use the `--recurse` parameter if you have lots of subfolders that need to be generated as well.
* You should use your own custom `--template` 


    .\clowncar.exe --path="~\my-notes" --output="~\my-website" --template="template.clowntent" --recurse

You'll see 1 line of output from clowncar about every single file it encounters. There are a few different lines you'll see:

Here's is an example of each type of output:

    ~~> example.html 1666 chars, template: template.clowntent
    ++> screenshot.jpg
    xx> (skipped) example.html

The meanings of each type of output are based on the first few characters, to wit:

 * `~~>` is used for a file that was generated
 * `++>` is used for a file that was copied to the output path. All files are copied except a few types: `.md`, `.html`, `.clowntent`, `.pre`, `.ok`, `.ps1`.
 * `XX>` is used for a file that is skipped -- it is not copied to the output path. In addition to the types listed above, the `.git` folder is also ignored. (Consider: node_modules folder should be ignored too.)

(Note that copying of files will only happen if an "output path" (`--output`, `-o`) is specified.)


### How to be safe... use `--dryrun`

There is also an option called "dry run" (`--dryrun` or just `-d`) that will not stop `clowncar` from actually changing any files. 

Instead, it will just show you what it *would* have done. (This is similar to the `-whatif` convention from Powershell.)

The messages sent to the console are the same, except that every line of text has `(dryrun)` before it. So for example you might see:

    (dryrun)~~> example.html 1666 chars, template: template.clowntent
    (dryrun)++> screenshot.jpg
    (dryrun)xx> (skipped) example.html

When the `--dryrun` flag is set, none of the actions are actually performed.

Our example above would become:

    .\clowncar.exe --path="~\my-notes" --output="~\my-website" --template="template.clowntent" --recurse --dryrun

## Live Demonstration

Here's a website I built using clowncar:

 * <https://til.secretgeek.net/> &mdash; website built with clowncar, based on [these markdown files](https://github.com/secretGeek/today-i-learned-staging)




## What's next?

I am not sure. I need to add a contributing file, blog about it, add some more features, survive this global pandemic, get my life in order. Y'know... the usual.