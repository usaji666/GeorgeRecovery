from pathlib import Path

from PIL import Image


PROJECT = Path("/Users/wanghan/Documents/治疗乔治腿伤Mod/GeorgeRecovery")
GEORGE = PROJECT / "_build/original-assets/sprites/George-1.png"
LEWIS = PROJECT / "_build/original-assets/sprites/Lewis-1.png"
OUTPUT = PROJECT / "assets/George_Standing.png"
PREVIEW = PROJECT / "_build/imagegen/George_Standing-v11-preview-8x.png"

# Lewis uses the game's native slim body and standard four-direction walking
# cadence. Map his palette to George's existing green/blue/brown colors.
PALETTE = {
    (255, 194, 140, 255): (249, 187, 151, 255),
    (242, 129, 84, 255): (207, 141, 112, 255),
    (183, 82, 55, 255): (154, 97, 74, 255),
    (63, 178, 0, 255): (71, 157, 81, 255),
    (25, 123, 26, 255): (37, 84, 67, 255),
    (20, 58, 33, 255): (27, 47, 44, 255),
    (254, 231, 30, 255): (47, 52, 108, 255),
    (210, 136, 20, 255): (26, 30, 83, 255),
    (103, 74, 15, 255): (14, 16, 63, 255),
    (55, 40, 4, 255): (14, 16, 63, 255),
}

HEAD_COLORS = {
    (102, 53, 55, 255),
    (249, 187, 151, 255),
    (207, 141, 112, 255),
    (154, 97, 74, 255),
    (253, 255, 221, 255),
    (33, 33, 33, 255),
    (71, 71, 71, 255),
    (92, 92, 92, 255),
    (100, 100, 100, 255),
    (119, 119, 119, 255),
    (156, 156, 156, 255),
}
LOWER_FACE_COLORS = {
    (102, 53, 55, 255),
    (249, 187, 151, 255),
    (207, 141, 112, 255),
    (154, 97, 74, 255),
}


def keep_largest_component(image: Image.Image) -> Image.Image:
    """Remove disconnected wheelchair pixels while keeping the complete head."""
    remaining = {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if image.getpixel((x, y))[3] > 0
    }
    components: list[set[tuple[int, int]]] = []

    while remaining:
        start = remaining.pop()
        component = {start}
        stack = [start]
        while stack:
            pixel_x, pixel_y = stack.pop()
            for neighbor in (
                (pixel_x - 1, pixel_y),
                (pixel_x + 1, pixel_y),
                (pixel_x, pixel_y - 1),
                (pixel_x, pixel_y + 1),
            ):
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    component.add(neighbor)
                    stack.append(neighbor)
        components.append(component)

    if not components:
        return image

    keep = max(components, key=len)
    for pixel_y in range(image.height):
        for pixel_x in range(image.width):
            if (pixel_x, pixel_y) not in keep:
                image.putpixel((pixel_x, pixel_y), (0, 0, 0, 0))
    return image


def round_side_back(image: Image.Image, direction: int) -> None:
    """Fill the lower rear outline with skin so both side views stay round."""
    skin_dark = (154, 97, 74, 255)
    skin_mid = (207, 141, 112, 255)
    skin_light = (249, 187, 151, 255)
    if direction == 1:  # facing right: rear is on the left
        rows = {
            8: ((3, skin_dark), (4, skin_mid), (5, skin_light)),
            9: ((4, skin_dark), (5, skin_mid), (6, skin_light)),
            10: ((5, skin_dark), (6, skin_mid), (7, skin_light), (8, skin_mid)),
        }
    elif direction == 3:  # facing left: rear is on the right
        rows = {
            8: ((10, skin_light), (11, skin_mid), (12, skin_dark)),
            9: ((9, skin_light), (10, skin_mid), (11, skin_dark)),
            10: ((7, skin_mid), (8, skin_light), (9, skin_mid), (10, skin_dark)),
        }
    else:
        return

    for pixel_y, pixels in rows.items():
        for pixel_x, color in pixels:
            image.putpixel((pixel_x, pixel_y), color)


def round_and_clean_side_rear(image: Image.Image, direction: int) -> None:
    """Round the side silhouette and remove dark pixels below George's ear."""
    transparent = (0, 0, 0, 0)
    gray_dark = (71, 71, 71, 255)
    gray_fill = (119, 119, 119, 255)
    skin_mid = (207, 141, 112, 255)
    dark_colors = {
        (33, 33, 33, 255),
        (71, 71, 71, 255),
        (154, 97, 74, 255),
    }

    # Coordinates are local to the extracted head. The contour expands through
    # the middle of the skull, then steps inward under the ear to form an oval.
    left_contour = (5, 4, 3, 2, 2, 3, 4, 4, 4, 5, 6)
    if direction not in (1, 3):
        return

    for pixel_y, left_edge in enumerate(left_contour[: image.height]):
        rear_edge = left_edge if direction == 1 else 15 - left_edge

        # Remove only pixels outside the target curve, then add the new edge.
        outside_pixels = range(0, rear_edge) if direction == 1 else range(rear_edge + 1, image.width)
        for pixel_x in outside_pixels:
            image.putpixel((pixel_x, pixel_y), transparent)

        edge_color = gray_dark if pixel_y <= 5 else skin_mid
        image.putpixel((rear_edge, pixel_y), edge_color)

        # Below the ear, replace every black/dark-gray/dark-skin pixel on the
        # rear half. This includes the three diagonal blocks at the bottom.
        if pixel_y >= 6:
            rear_pixels = range(rear_edge, 8) if direction == 1 else range(8, rear_edge + 1)
            for pixel_x in rear_pixels:
                if image.getpixel((pixel_x, pixel_y)) in dark_colors:
                    image.putpixel((pixel_x, pixel_y), skin_mid)
        else:
            rear_pixels = range(rear_edge + 1, 8) if direction == 1 else range(8, rear_edge)
            for pixel_x in rear_pixels:
                if image.getpixel((pixel_x, pixel_y)) in dark_colors:
                    image.putpixel((pixel_x, pixel_y), gray_fill)

    # These are the actual three diagonal blocks visible below the ear. They
    # come from George's dark-brown wheelchair sprite, not from the gray outer
    # contour, so handle their exact mirrored coordinates explicitly.
    under_ear_pixels = ((5, 7), (6, 8), (7, 9)) if direction == 1 else ((10, 7), (9, 8), (8, 9))
    for pixel_x, pixel_y in under_ear_pixels:
        image.putpixel((pixel_x, pixel_y), skin_mid)


def soften_back_hair(image: Image.Image) -> None:
    """Widen the middle of the rear head so both sides form a clear curve."""
    transparent = (0, 0, 0, 0)
    gray_dark = (71, 71, 71, 255)
    gray_mid = (119, 119, 119, 255)
    gray_light = (156, 156, 156, 255)
    hair_brown = (102, 53, 55, 255)

    # Widen the top cap from four to six pixels, followed by the original
    # eight- and ten-pixel rows, so the rear head begins with a round crown.
    image.putpixel((5, 0), hair_brown)
    image.putpixel((10, 0), hair_brown)

    # Keep the crown at six/eight/ten pixels, then widen the middle to twelve.
    # The lower skin later narrows through ten/eight/six pixels, producing a
    # visibly rounded left and right silhouette instead of a vertical block.
    for pixel_y in range(3, 8):
        for pixel_x in range(image.width):
            if pixel_y >= 5 or pixel_x < 2 or pixel_x > 13:
                image.putpixel((pixel_x, pixel_y), transparent)

    for pixel_y in (5, 6, 7):
        left, right = 2, 13
        for pixel_x in range(left, right + 1):
            distance_to_edge = min(pixel_x - left, right - pixel_x)
            if distance_to_edge == 0:
                color = gray_dark
            elif pixel_y == 7:
                color = gray_mid
            elif pixel_y == 6 and distance_to_edge >= 2:
                color = gray_light
            else:
                color = gray_mid if distance_to_edge <= 2 else gray_light
            image.putpixel((pixel_x, pixel_y), color)


def make_back_skin() -> Image.Image:
    """Round the lower rear head from ten to eight to six pixels."""
    skin_dark = (154, 97, 74, 255)
    skin_mid = (207, 141, 112, 255)
    skin_light = (249, 187, 151, 255)
    image = Image.new("RGBA", (16, 3), (0, 0, 0, 0))
    for pixel_y, (left, right) in enumerate(((3, 12), (4, 11), (5, 10))):
        for pixel_x in range(left, right + 1):
            distance_to_edge = min(pixel_x - left, right - pixel_x)
            color = skin_dark if distance_to_edge == 0 else skin_mid if distance_to_edge == 1 else skin_light
            image.putpixel((pixel_x, pixel_y), color)
    return image

george = Image.open(GEORGE).convert("RGBA")
lewis = Image.open(LEWIS).convert("RGBA")
sheet = Image.new("RGBA", (64, 128), (0, 0, 0, 0))

for direction in range(4):
    for frame_index in range(4):
        x = frame_index * 16
        y = direction * 32
        frame = lewis.crop((x, y, x + 16, y + 32))
        frame.putdata([PALETTE.get(pixel, pixel) for pixel in frame.getdata()])

        # Remove Lewis's head and beard while retaining his narrow native torso.
        frame.paste((0, 0, 0, 0), (0, 0, 16, 16))

        # George's wheelchair sprite places his head lower than a standing NPC.
        # For the back view, extend only the original lower gray hair to the
        # standing height so the lower head reaches the collar without a hole.
        # The other views include the original chin and beard rows.
        head_bottom = y + 15 if direction == 2 else y + 18
        head = george.crop((x, y + 7, x + 16, head_bottom))
        for head_y in range(head.height):
            allowed = HEAD_COLORS if direction == 2 or head_y <= 7 else LOWER_FACE_COLORS
            for head_x in range(head.width):
                if head.getpixel((head_x, head_y)) not in allowed:
                    head.putpixel((head_x, head_y), (0, 0, 0, 0))
        head = keep_largest_component(head)
        round_side_back(head, direction)
        round_and_clean_side_rear(head, direction)
        if direction == 2:
            soften_back_hair(head)
        frame.alpha_composite(head, (0, 5))
        if direction == 2:
            frame.alpha_composite(make_back_skin(), (0, 13))

        # Compact only the lower body by two pixels. George keeps the game's
        # native walking poses, but his trousers no longer make his legs look
        # disproportionately long.
        compact_legs = frame.crop((0, 22, 16, 32)).resize((16, 8), Image.Resampling.NEAREST)
        frame.paste((0, 0, 0, 0), (0, 22, 16, 32))
        frame.alpha_composite(compact_legs, (0, 22))

        # The completed head ends on row 15 and touches the native torso on row
        # 16 directly. No neck or extra collar pixels are drawn.

        # Native walking frames already alternate arms and legs. Lift both step
        # frames by one pixel so George's careful gait has a visible rhythmic bob.
        target_y = y - 1 if frame_index in (1, 3) else y
        sheet.alpha_composite(frame, (x, target_y))

sheet.save(OUTPUT)
sheet.resize((512, 1024), Image.Resampling.NEAREST).save(PREVIEW)

corner_alpha = [sheet.getpixel(point)[3] for point in ((0, 0), (63, 0), (0, 127), (63, 127))]
frame_bounds = [
    sheet.crop((column * 16, row * 32, column * 16 + 16, row * 32 + 32)).getchannel("A").getbbox()
    for row in range(4)
    for column in range(4)
]
print(f"wrote={OUTPUT}")
print(f"size={sheet.size[0]}x{sheet.size[1]}")
print(f"corner_alpha={corner_alpha}")
print(f"frame_bounds={frame_bounds}")
