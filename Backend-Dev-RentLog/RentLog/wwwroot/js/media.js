window.rentLogMedia = {
    compressSelectedImage: async function (container, fileIndex, mimeType, maxWidth, maxHeight) {
        const file = container.querySelector("input[type=file]").files[fileIndex];
        if (!file) {
            throw new Error("Selected image is no longer available.");
        }

        const image = await createImageBitmap(file);
        const scale = Math.min(1, maxWidth / image.width, maxHeight / image.height);
        const canvas = document.createElement("canvas");
        canvas.width = Math.max(1, Math.round(image.width * scale));
        canvas.height = Math.max(1, Math.round(image.height * scale));

        const context = canvas.getContext("2d");
        if (!context) {
            image.close();
            throw new Error("Image processing is unavailable.");
        }

        context.fillStyle = "#ffffff";
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.drawImage(image, 0, 0, canvas.width, canvas.height);
        image.close();
        const outputType = mimeType === "image/png" ? "image/png" : "image/jpeg";
        return canvas.toDataURL(outputType, 0.8).split(",", 2)[1];
    },
    getVideoDurationFromInput: async function (container, fileIndex) {
        const file = container.querySelector("input[type=file]").files[fileIndex];
        if (!file) {
            throw new Error("Selected video is no longer available.");
        }
        const video = document.createElement("video");
        video.preload = "metadata";
        const objectUrl = URL.createObjectURL(file);
        try {
            return await new Promise((resolve, reject) => {
                video.onloadedmetadata = () => resolve(video.duration);
                video.onerror = () => reject(new Error("Video metadata could not be read."));
                video.src = objectUrl;
            });
        } finally {
            URL.revokeObjectURL(objectUrl);
        }
    }
};
