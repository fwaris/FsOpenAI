
function fso_scrollTo(elementId) {
    var element = document.getElementById(elementId);
    //elementId.parentNode.scrollTop = target.offsetTop;
    //element.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'start' });
    //element.scrollIntoView({ behavior: 'smooth', block: 'start', inline: 'start' });
    element.scrollIntoView(true)
}

function inputValue(elementId) {
    var element = document.getElementById(elementId)
    return element.value
}

window.hideLoadingScreen = function () {
    var loading = document.getElementById('loading-screen');
    if (loading) {
        loading.style.opacity = '0';
        setTimeout(function () {
            loading.parentNode && loading.parentNode.removeChild(loading);
        }, 400); // adjust based on CSS transition
    }
}
