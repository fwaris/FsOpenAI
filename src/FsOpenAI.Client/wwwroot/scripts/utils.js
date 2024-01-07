
function fso_scrollTo(elementId) {
    var element = document.getElementById(elementId);
    //elementId.parentNode.scrollTop = target.offsetTop;
    //element.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'start' });
    //element.scrollIntoView({ behavior: 'smooth', block: 'start', inline: 'start' });
    element.scrollIntoView(true)
}
