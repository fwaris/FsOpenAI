
function fso_scrollTo(elementId) {
    var element = document.getElementById(elementId);
    element.scrollIntoView({
        behavior: 'smooth'
    });
}