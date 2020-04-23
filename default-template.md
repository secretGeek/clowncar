## Default Template

This is the default template that is used if no template is provided.

    <!doctype html>
    <html lang='en'>
    <head>
    <meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{{title}}</title>
    <link rel='icon' href='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 100 100%22><text y=%22.9em%22 font-size=%2290%22>🤡</text></svg>'>
    </head>
    <body>
    <style>
    html {
        background-color:#FFF;
        color:#333;
    }
    body {
        max-width:70ch;
        padding:2ch;
        margin:auto;
    }
    pre,blockquote {
        margin-left:4ch;
        margin-right:0;
        background-color:#EEE;
        padding:1ch;
    }
    pre {
        white-space:pre-wrap;
    }
    blockquote {
        border-left:1ch solid #AAA;
    }
    @media (prefers-color-scheme: dark) {
        html {
            filter: invert(100%);
        }
        img:not(.ignore-color-scheme) {
            filter: brightness(50%) invert(100%);
        }
        .ignore-color-scheme {
            filter: invert(100%);
        }
    }
    </style>
    {{body}}
    </body>
    </html>

## Features

First, we scale the `viewport`, to help on mobile devices:

    <meta charset='utf-8' name='viewport' content='width=device-width, initial-scale=1.0'>

Next, we use `svg` to set an `emoji` as the favicon, as described at [Use an Emoji as a favicon](https://til.secretgeek.net/html/emoji_favicon.html).

The initial basis of the styles is [58 bytes of css to look great nearly everywhere](https://jrl.ninja/etc/1/) which is responsible for:

    body {
        max-width:70ch;
        padding:2ch;
        margin:auto;
    }

In addition, I found that `pre` and `blockquote` deserve a little bit of styling, so they've been given just a sprinkling:

    pre,blockquote {
        margin-left:4ch;
        margin-right:0;
        background-color:#EEE;
        padding:1ch;
    }
    pre {
        white-space:pre-wrap;
    }
    blockquote {
        border-left:1ch solid #AAA;
    }


Finally I wanted to the make the default template minimalist, but still support dark theme, so I've used this [poor man's dark mode css](https://til.secretgeek.net/css/dark_mode_css.html).



    @media (prefers-color-scheme: dark) {
        html {
            filter: invert(100%);
        }
        img:not(.ignore-color-scheme) {
            filter: brightness(50%) invert(100%);
        }
        .ignore-color-scheme {
            filter: invert(100%);
        }
    }

If the user has applied dark mode to their device, then all of the pages colors will be inverted. And images will be inverted a second time (i.e. left alone) but have their brightness turned down, so they aren't blinding.

Note there is an unused class, called `.ignore-color-scheme` which you can apply to any element that you don't want dark-mode to affect. For example if exact colors are important.

And to make the dark mode theme work, we must explicitly set background-color on the `html` element.

    html {
        background-color:#FFF;
        color:#333;
    }

