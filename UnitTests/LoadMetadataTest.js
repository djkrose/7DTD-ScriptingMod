  

// This is a test script file for Unit tests to test metadata loading.
// The first empty lines above should be ignored, so should these comments before any tags.
// 
// @commands           test
// @defaultPermission  0
// @description        Some example command
// @help               This is a multi-line help section. It automatically adjusts indentation based on the first line.
//                     Examples:
//                       1. Should start with two spaces
//                   2. Relative negative indentation should be ignored, i.e. content not truncated.
//
//                     Empty lines, like the one above, should be preserved.

// Help text has ended here because the comment block is interrupted by a newline without comment.
// @shouldBeIgnored    ignored

console.log("Hello World!");
