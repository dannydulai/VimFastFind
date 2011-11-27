#!/bin/sh

make -j9 config=release
mkdir -p ~/.vim/plugin/VFF
cp VFF.vim ~/.vim/plugin/
cp bin/release/*.exe ~/.vim/plugin/VFF
