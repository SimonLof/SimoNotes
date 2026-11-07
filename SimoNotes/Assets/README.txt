SimoNotes.ico is a temporary notification-area icon. Replace it with a custom
multi-size .ico when the final artwork is ready.

An .ico file can contain multiple images. Include 16, 20, 24, 32, 48, and 256
pixel square versions so Windows can choose a crisp image for different DPI
settings. Keep transparent pixels in the artwork; Windows selects the best
matching size automatically.

Keep an SVG or other vector source, then export the sizes into one .ico. With
ImageMagick, for example:

  magick source.png -define icon:auto-resize=256,128,64,48,32,24,20,16 SimoNotes.ico

Replace Assets\SimoNotes.ico. The project copies it beside the application on
build.

Terminal notes use Press Start 2P, a pixel font distributed under the SIL Open
Font License. The license is included in PressStart2P-OFL.txt; keep it with the
font asset if the font is redistributed.