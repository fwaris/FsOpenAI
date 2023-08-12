function appendText(elemId,txt) {
    const textarea = document.getElementById(elemId);
    textarea.value += txt;   
}

function scrollToEnd(textarea) {
    textarea.scrollTop = textarea.scrollHeight;
}

function scrollTo(elementId) {
    var element = document.getElementById(elementId);
    element.scrollIntoView({
        behavior: 'smooth'
    });
}