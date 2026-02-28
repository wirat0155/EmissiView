// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
const basePath = (window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1') ?  "/" + window.location.pathname.split('/')[1] : ''; // Adjust '/myapp' to your subfolder name
