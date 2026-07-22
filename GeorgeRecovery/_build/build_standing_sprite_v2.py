"""Build George's standing sprite sheet from the vanilla game art.

George only exists as a seated wheelchair sprite, so the standing sheet grafts
his vanilla head onto Lewis's standing body (Lewis uses the game's native slim
build and four-direction walk cadence).

Two rules keep the head looking like George instead of like a smudge:

1. The head is cut out by *position* (a fixed row range), never by redrawing its
   silhouette. George's face outline is dark brown (102, 53, 55) and the back of
   his head in the side views is outlined in black (33, 33, 33) - both are part
   of the artwork, so any pass that "cleans up dark pixels" erases the outline
   itself. There is no such pass here.
2. Wheelchair and jacket pixels are dropped because their colours are simply not
   in HEAD_COLORS, and any stray bit of chair frame that shares the head's black
   is removed by keeping only the largest connected blob. No coordinate lists.

The only hand-drawn pixels in this file are the lower half of the back of the
head: the vanilla back view hides everything below George's bald crown behind
the wheelchair's push bar, so BACK_HEAD_ROWS rebuilds his grey hair ring.

Run it from anywhere:

    python3 GeorgeRecovery/_build/build_standing_sprite_v2.py

It needs the vanilla George and Lewis sprite sheets under
`GeorgeRecovery/_build/original-assets/sprites/`. Those are game files, so they
stay out of the repository (see RELEASE_PROCESS.md).
"""

from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
SOURCE_DIR = PROJECT / "_build/original-assets/sprites"
OUTPUT = PROJECT / "assets/George_Standing.png"
PREVIEW = PROJECT / "_build/imagegen/George_Standing-preview-8x.png"

FRAME_WIDTH = 16
FRAME_HEIGHT = 32
DIRECTION_UP = 2

# Where George's head sits inside his vanilla wheelchair frame, and where it has
# to land on Lewis's standing body so the chin meets the collar on row 15.
HEAD_TOP = 7
HEAD_BOTTOM = 18        # exclusive; row 17 is the bottom of his beard
BACK_HEAD_BOTTOM = 12   # exclusive; below this the wheelchair's push bar starts
HEAD_PASTE_Y = 5
COLLAR_ROW = 15         # last row of Lewis's head, and his collar in side views

# Lewis's palette mapped onto George's greens, browns and blues.
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

# Every colour George's head is drawn with. The wheelchair's greys (76, 76, 76)
# and (92, 92, 92), its black (22, 22, 22), its blues and the jacket greens are
# deliberately absent, so they disappear without touching a single coordinate.
HEAD_COLORS = {
    (102, 53, 55, 255),    # hair, eyebrows, and the face outline
    (33, 33, 33, 255),     # brows, glasses, and the side-view rear outline
    (249, 187, 151, 255),  # light skin
    (207, 141, 112, 255),  # mid skin
    (154, 97, 74, 255),    # shaded skin
    (71, 71, 71, 255),     # grey hair, darkest
    (100, 100, 100, 255),
    (119, 119, 119, 255),
    (156, 156, 156, 255),  # grey hair, lightest
    (253, 255, 221, 255),  # eye white
}

OUTLINE = (102, 53, 55, 255)
HAIR_DARK = (71, 71, 71, 255)
HAIR_MID = (119, 119, 119, 255)
HAIR_LIGHT = (156, 156, 156, 255)
SKIN_MID = (207, 141, 112, 255)
SKIN_LIGHT = (249, 187, 151, 255)

# The lower back of George's head, rebuilt because the wheelchair hides it.
# Rows 0-4 come from the game; these six continue it. The grey ring grows in
# from both edges as a horseshoe so the bald crown tapers to a point instead of
# stopping on a flat band, and the silhouette matches the front view row for row.
BACK_HEAD_ROWS = {
    5: ((2, OUTLINE), (3, HAIR_MID), (4, HAIR_LIGHT), (5, SKIN_MID), (6, SKIN_LIGHT),
        (7, SKIN_LIGHT), (8, SKIN_LIGHT), (9, SKIN_LIGHT), (10, SKIN_MID),
        (11, HAIR_LIGHT), (12, HAIR_MID), (13, OUTLINE)),
    6: ((2, OUTLINE), (3, HAIR_DARK), (4, HAIR_MID), (5, HAIR_LIGHT), (6, SKIN_MID),
        (7, SKIN_LIGHT), (8, SKIN_LIGHT), (9, SKIN_MID), (10, HAIR_LIGHT),
        (11, HAIR_MID), (12, HAIR_DARK), (13, OUTLINE)),
    7: ((3, OUTLINE), (4, HAIR_DARK), (5, HAIR_MID), (6, HAIR_LIGHT), (7, SKIN_MID),
        (8, SKIN_MID), (9, HAIR_LIGHT), (10, HAIR_MID), (11, HAIR_DARK), (12, OUTLINE)),
    8: ((3, OUTLINE), (4, SKIN_LIGHT), (5, HAIR_DARK), (6, HAIR_MID), (7, HAIR_MID),
        (8, HAIR_MID), (9, HAIR_MID), (10, HAIR_DARK), (11, SKIN_LIGHT), (12, OUTLINE)),
    9: ((3, OUTLINE), (4, SKIN_MID), (5, HAIR_DARK), (6, HAIR_DARK), (7, HAIR_MID),
        (8, HAIR_MID), (9, HAIR_DARK), (10, HAIR_DARK), (11, SKIN_MID), (12, OUTLINE)),
    10: ((4, OUTLINE), (5, OUTLINE), (6, SKIN_MID), (7, SKIN_LIGHT), (8, SKIN_LIGHT),
         (9, SKIN_MID), (10, OUTLINE), (11, OUTLINE)),
}

# The nape, for the same reason: on the jaw row George's jacket collar rises up
# behind his head, so the vanilla art simply has no hair there. Dropping the
# jacket left the back of the skull caving in by three pixels in one row. These
# two pixels per side carry the silhouette down at one pixel per row, the same
# taper Lewis's own head uses. Keyed by direction, in head-local coordinates.
SIDE_NAPE_ROWS = {
    1: {9: ((5, HAIR_DARK), (6, HAIR_MID))},   # facing right: nape on the left
    3: {9: ((10, HAIR_DARK), (9, HAIR_MID))},  # facing left: nape on the right
}


def load_source(name: str) -> Image.Image:
    """Find a vanilla sheet, whichever export suffix it was saved with."""
    for candidate in (f"{name}-1.png", f"{name}.png", f"{name}-0.png"):
        path = SOURCE_DIR / candidate
        if path.exists():
            return Image.open(path).convert("RGBA")
    raise SystemExit(
        f"Missing the vanilla {name} sprite sheet.\n"
        f"Unpack Content/Characters/{name}.xnb and save it as\n"
        f"  {SOURCE_DIR / f'{name}.png'}\n"
        "Game files are not stored in this repository; see RELEASE_PROCESS.md."
    )


def keep_largest_component(image: Image.Image) -> Image.Image:
    """Drop wheelchair fragments that survived the colour filter but float free."""
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


def drop_orphan_runs(image: Image.Image) -> Image.Image:
    """Drop the slivers of hair the seated jacket collar cuts off from the head.

    In the side views George's collar rises in front of his grey hair, so once
    the jacket colours are removed a lone hair pixel is left stranded with a
    hole beside it. Keeping only each row's widest run removes the sliver
    without touching the face, which is always the widest run in its row.
    """
    for pixel_y in range(image.height):
        runs: list[list[int]] = []
        run: list[int] = []
        for pixel_x in range(image.width):
            if image.getpixel((pixel_x, pixel_y))[3] > 0:
                run.append(pixel_x)
            elif run:
                runs.append(run)
                run = []
        if run:
            runs.append(run)
        if len(runs) < 2:
            continue
        widest = max(runs, key=len)
        for other in runs:
            if other is widest:
                continue
            for pixel_x in other:
                image.putpixel((pixel_x, pixel_y), (0, 0, 0, 0))
    return image


def extract_head(george: Image.Image, direction: int, frame_index: int) -> Image.Image:
    """Cut George's head out of his wheelchair frame with its outline intact."""
    left = frame_index * FRAME_WIDTH
    top = direction * FRAME_HEIGHT + HEAD_TOP
    bottom = direction * FRAME_HEIGHT + (
        BACK_HEAD_BOTTOM if direction == DIRECTION_UP else HEAD_BOTTOM
    )
    head = george.crop((left, top, left + FRAME_WIDTH, bottom))

    for pixel_y in range(head.height):
        for pixel_x in range(head.width):
            if head.getpixel((pixel_x, pixel_y)) not in HEAD_COLORS:
                head.putpixel((pixel_x, pixel_y), (0, 0, 0, 0))
    head = drop_orphan_runs(keep_largest_component(head))

    if direction == DIRECTION_UP:
        # Only his bald crown survives in the vanilla back view; rebuild the rest.
        full = Image.new("RGBA", (FRAME_WIDTH, HEAD_BOTTOM - HEAD_TOP), (0, 0, 0, 0))
        full.alpha_composite(head, (0, 0))
        for pixel_y, pixels in BACK_HEAD_ROWS.items():
            for pixel_x, color in pixels:
                full.putpixel((pixel_x, pixel_y), color)
        head = full

    for pixel_y, pixels in SIDE_NAPE_ROWS.get(direction, {}).items():
        for pixel_x, color in pixels:
            head.putpixel((pixel_x, pixel_y), color)

    return head


def frame_bob(lewis: Image.Image, direction: int, frame_index: int) -> int:
    """How far Lewis's body already dips on this walk frame.

    His two step frames sit one pixel lower than his standing frames - that is
    the game's own walk bob. George's head has to ride along with it, otherwise
    the head floats off the shoulders on every other frame.
    """
    top = direction * FRAME_HEIGHT

    def head_top(index: int) -> int:
        cell = lewis.crop((index * FRAME_WIDTH, top, index * FRAME_WIDTH + FRAME_WIDTH, top + FRAME_HEIGHT))
        return cell.getchannel("A").getbbox()[1]

    return head_top(frame_index) - head_top(0)


def build_frame(george: Image.Image, lewis: Image.Image, direction: int, frame_index: int) -> Image.Image:
    left = frame_index * FRAME_WIDTH
    top = direction * FRAME_HEIGHT
    frame = lewis.crop((left, top, left + FRAME_WIDTH, top + FRAME_HEIGHT))
    frame.putdata([PALETTE.get(pixel, pixel) for pixel in frame.getdata()])

    bob = frame_bob(lewis, direction, frame_index)
    collar_row = COLLAR_ROW + bob

    # Remove Lewis's head and beard while retaining his narrow native torso.
    # His collar row is his own beard when facing down, so keep only the pixels
    # whose colour is also worn on the shoulders. That closes the notch under
    # the back of George's head; anything left over ends up behind his chin.
    frame.paste((0, 0, 0, 0), (0, 0, FRAME_WIDTH, collar_row))
    shoulders = {
        frame.getpixel((pixel_x, pixel_y))
        for pixel_y in (collar_row + 1, collar_row + 2)
        for pixel_x in range(FRAME_WIDTH)
        if frame.getpixel((pixel_x, pixel_y))[3] > 0
    }
    for pixel_x in range(FRAME_WIDTH):
        if frame.getpixel((pixel_x, collar_row)) not in shoulders:
            frame.putpixel((pixel_x, collar_row), (0, 0, 0, 0))

    frame.alpha_composite(extract_head(george, direction, frame_index), (0, HEAD_PASTE_Y + bob))

    # Compact only the lower body by two pixels. George keeps the game's native
    # walking poses, but his trousers no longer make his legs look too long.
    compact_legs = frame.crop((0, 22, FRAME_WIDTH, 32)).resize((FRAME_WIDTH, 8), Image.Resampling.NEAREST)
    frame.paste((0, 0, 0, 0), (0, 22, FRAME_WIDTH, 32))
    frame.alpha_composite(compact_legs, (0, 22))
    return frame


def main() -> None:
    george = load_source("George")
    lewis = load_source("Lewis")
    sheet = Image.new("RGBA", (64, 128), (0, 0, 0, 0))

    for direction in range(4):
        for frame_index in range(4):
            frame = build_frame(george, lewis, direction, frame_index)
            # The native frames already alternate arms and legs and dip on each
            # step, so they are placed exactly where the game drew them.
            sheet.alpha_composite(frame, (frame_index * FRAME_WIDTH, direction * FRAME_HEIGHT))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(OUTPUT)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    sheet.resize((512, 1024), Image.Resampling.NEAREST).save(PREVIEW)

    print(f"wrote={OUTPUT}")
    print(f"size={sheet.size[0]}x{sheet.size[1]}")
    for direction, name in enumerate(("down", "right", "up", "left")):
        outline = sum(
            1
            for y in range(direction * FRAME_HEIGHT, direction * FRAME_HEIGHT + 16)
            for x in range(FRAME_WIDTH)
            if sheet.getpixel((x, y)) in (OUTLINE, (33, 33, 33, 255))
        )
        print(f"{name}: {outline} outline pixels in the head")


if __name__ == "__main__":
    main()
