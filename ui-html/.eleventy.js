const htmlmin = require("html-minifier");

module.exports = function (config) {
    config.addPassthroughCopy('./src/img');
    config.addPassthroughCopy('./src/site.webmanifest');
    config.addPassthroughCopy('./src/robots.txt');
    config.setServerOptions({
      watch: ['./_site/o_0.css']
    });

    // HTML minify
    config.addTransform("htmlmin", (content, outputPath) => {
        if (outputPath.endsWith(".html")) {
        return htmlmin.minify(content, {
            collapseWhitespace: true,
            removeComments: true,
            useShortDoctype: true,
            minifyJS: true,
        });
        }
        return content;
    });

    return {
        passthroughFileCopy: true,
        dir: {
            input: "src",
            includes: "_includes",
            data: "_data",
            output: "_site",
        },
    };
};
