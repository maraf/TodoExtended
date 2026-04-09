let recognition = null;

export function isSupported() {
    return 'SpeechRecognition' in window || 'webkitSpeechRecognition' in window;
}

export function startListening(dotNetRef) {
    if (recognition) {
        recognition.abort();
        recognition = null;
    }

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) return;

    recognition = new SpeechRecognition();
    recognition.lang = navigator.language || 'en-US';
    recognition.interimResults = false;
    recognition.continuous = false;

    recognition.onresult = (event) => {
        const transcript = event.results[0][0].transcript;
        dotNetRef.invokeMethodAsync('OnSpeechResult', transcript);
    };

    recognition.onerror = () => {
        recognition = null;
        dotNetRef.invokeMethodAsync('OnSpeechEnded');
    };

    recognition.onend = () => {
        recognition = null;
        dotNetRef.invokeMethodAsync('OnSpeechEnded');
    };

    recognition.start();
}

export function stopListening() {
    if (recognition) {
        recognition.abort();
        recognition = null;
    }
}
