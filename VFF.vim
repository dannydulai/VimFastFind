" The key sequence that should activate the buffer browser. The default is ^F.
"   Enter the key sequence in a single quoted string, exactly as you would use
"   it in a map command.
"

:if !exists("g:vffFindActKeySeq")
:    let vffFindActKeySeq = '<C-F>'
:endif

:if !exists("g:vffGrepActKeySeq")
:    let vffGrepActKeySeq = '<C-E>'
:endif

" The name of the browser. The default is "/---Select File---", but you can
"   change the name at your will. A leading '/' is advised if you change
"   directories from with in vim.
:let vffWindowName = '/---\ Select\ File\ ---'

" A non-zero value for the variable vffRemoveBrowserBuffer means that after
"   the selection is made, the buffer that belongs to the browser should be
"   deleted. But this is not advisable as vim doesn't reuse the buffer numbers
"   that are no longer used. The default value is 0, i.e., reuse a single
"   buffer. This will avoid creating lots of gaps and quickly reach a large
"   buffer numbers for the new buffers created.
:let vffRemoveBrowserBuffer = 1

"
" A non-zero value for the variable highlightOnlyFilename will highlight only
"   the filename instead of the whole path. The default value is 0.
:let highlightOnlyFilename = 0


" Your can configure a delay in between when typing stops and results list.
"   To enable the delay, add to your .vimrc:
"      let g:vff_debounce = 1
"   The default is 100 ms. To change it to 50 ms, add to your .vimrc:
"      let g:vff_debounce_delay = 50
"
:if exists("g:vff_debounce")
:  if exists("g:vff_debounce_delay")
:    let g:vff_refreshdelay = g:vff_debounce_delay
:  else
:    let g:vff_refreshdelay = 100
:  endif
:endif

"
" END configuration.
"

:function! VffSetupActivationKey ()
:  exec 'nnoremap ' . g:vffFindActKeySeq . ' :call VffListBufs ("find")<CR>'
:  exec 'vnoremap ' . g:vffFindActKeySeq . ' :call VffListBufs ("find")<CR>'
:  exec 'nnoremap ' . g:vffGrepActKeySeq . ' :call VffListBufs ("grep")<CR>'
:  exec 'vnoremap ' . g:vffGrepActKeySeq . ' :call VffListBufs ("grep")<CR>'
:endfunction

:function! VffSetupDeActivationKey ()
:  exec 'nnoremap ' . g:vffFindActKeySeq . ' :call VffQuit ()<CR>:echo<CR>'
:  exec 'vnoremap ' . g:vffFindActKeySeq . ' :call VffQuit ()<CR>:echo<CR>'
:  exec 'nnoremap ' . g:vffGrepActKeySeq . ' :call VffQuit ()<CR>:echo<CR>'
:  exec 'vnoremap ' . g:vffGrepActKeySeq . ' :call VffQuit ()<CR>:echo<CR>'
:endfunction

:call VffSetupActivationKey ()

:let g:vff_lastline = -1

:function! VffListBufs (mode)
:  let g:vff_mode = a:mode
:  let g:vff_savetimeoutlen = &timeoutlen
:  let g:vff_origwin = winnr()
:  let l:saveReport = &report
:  let &timeoutlen=0
:  let &report=10000
:  split
:  setlocal noswapfile
:  silent! exec ":e " . g:vffWindowName
:  setlocal noswapfile
:  let g:vff_vffwin = winnr()
:  if g:vff_mode == 'find'
:     syn match Title "Find File:.*"
:  else
:     syn match Title "Find Content:.*"
:  endif
:  syn match Title "----------------*"
:  hi CursorLine   cterm=NONE ctermbg=darkblue ctermfg=white
:  setlocal cc=
:  let &report = l:saveReport
:  exec 'ruby $vff.enter("' . g:vff_mode . '")'
:  if g:vff_mode == 'grep'
:     if g:vff_lastline != -1
:        exec g:vff_lastline
:     endif
:  endif
:  set nomodified
:endfunction

:function! VffClearSetup ()
:  aug ListFiles
:    exec "au! WinEnter " . g:vffWindowName
:    exec "au! WinLeave " . g:vffWindowName
:    exec "au! BufLeave " . g:vffWindowName
:  aug END
:  call VffUnsetupSelect ()
:endfunction

:function! VffSetupBadSelect ()
:  if ! exists ("g:VffSetup")
:    nnoremap <buffer> <CR>     :call VffQuit()<CR>
:    nnoremap <buffer> <C-C>    :call VffQuit()<CR>
:    nnoremap <buffer> <ESC>    :call VffQuit()<CR>
:    call VffSetupDeActivationKey ()
:    let g:VffSetup = 1
:  endif
:endfunction

:function! VffSetupSelect ()
:  if ! exists ("g:VffSetup")
:    set nofoldenable
:    nnoremap <buffer> <CR>     :call VffSelectCurrentBuffer()<CR>
:    nnoremap <buffer> <C-C>    :call VffQuit()<CR>
:    nnoremap <buffer> <ESC>    :call VffQuit()<CR>
:    nnoremap <buffer> <SPACE>  :call VffText(' ')<CR>
:    nnoremap <buffer> a        :call VffText('a')<CR>
:    nnoremap <buffer> b        :call VffText('b')<CR>
:    nnoremap <buffer> c        :call VffText('c')<CR>
:    nnoremap <buffer> d        :call VffText('d')<CR>
:    nnoremap <buffer> e        :call VffText('e')<CR>
:    nnoremap <buffer> f        :call VffText('f')<CR>
:    nnoremap <buffer> g        :call VffText('g')<CR>
:    nnoremap <buffer> h        :call VffText('h')<CR>
:    nnoremap <buffer> i        :call VffText('i')<CR>
:    nnoremap <buffer> j        :call VffText('j')<CR>
:    nnoremap <buffer> k        :call VffText('k')<CR>
:    nnoremap <buffer> l        :call VffText('l')<CR>
:    nnoremap <buffer> m        :call VffText('m')<CR>
:    nnoremap <buffer> n        :call VffText('n')<CR>
:    nnoremap <buffer> o        :call VffText('o')<CR>
:    nnoremap <buffer> p        :call VffText('p')<CR>
:    nnoremap <buffer> q        :call VffText('q')<CR>
:    nnoremap <buffer> r        :call VffText('r')<CR>
:    nnoremap <buffer> s        :call VffText('s')<CR>
:    nnoremap <buffer> t        :call VffText('t')<CR>
:    nnoremap <buffer> u        :call VffText('u')<CR>
:    nnoremap <buffer> v        :call VffText('v')<CR>
:    nnoremap <buffer> w        :call VffText('w')<CR>
:    nnoremap <buffer> x        :call VffText('x')<CR>
:    nnoremap <buffer> y        :call VffText('y')<CR>
:    nnoremap <buffer> z        :call VffText('z')<CR>
:    nnoremap <buffer> A        :call VffText('A')<CR>
:    nnoremap <buffer> B        :call VffText('B')<CR>
:    nnoremap <buffer> C        :call VffText('C')<CR>
:    nnoremap <buffer> D        :call VffText('D')<CR>
:    nnoremap <buffer> E        :call VffText('E')<CR>
:    nnoremap <buffer> F        :call VffText('F')<CR>
:    nnoremap <buffer> G        :call VffText('G')<CR>
:    nnoremap <buffer> H        :call VffText('H')<CR>
:    nnoremap <buffer> I        :call VffText('I')<CR>
:    nnoremap <buffer> J        :call VffText('J')<CR>
:    nnoremap <buffer> K        :call VffText('K')<CR>
:    nnoremap <buffer> L        :call VffText('L')<CR>
:    nnoremap <buffer> M        :call VffText('M')<CR>
:    nnoremap <buffer> N        :call VffText('N')<CR>
:    nnoremap <buffer> O        :call VffText('O')<CR>
:    nnoremap <buffer> P        :call VffText('P')<CR>
:    nnoremap <buffer> Q        :call VffText('Q')<CR>
:    nnoremap <buffer> R        :call VffText('R')<CR>
:    nnoremap <buffer> S        :call VffText('S')<CR>
:    nnoremap <buffer> T        :call VffText('T')<CR>
:    nnoremap <buffer> U        :call VffText('U')<CR>
:    nnoremap <buffer> V        :call VffText('V')<CR>
:    nnoremap <buffer> W        :call VffText('W')<CR>
:    nnoremap <buffer> X        :call VffText('X')<CR>
:    nnoremap <buffer> Y        :call VffText('Y')<CR>
:    nnoremap <buffer> Z        :call VffText('Z')<CR>
:    nnoremap <buffer> 0        :call VffText('0')<CR>
:    nnoremap <buffer> 1        :call VffText('1')<CR>
:    nnoremap <buffer> 2        :call VffText('2')<CR>
:    nnoremap <buffer> 3        :call VffText('3')<CR>
:    nnoremap <buffer> 4        :call VffText('4')<CR>
:    nnoremap <buffer> 5        :call VffText('5')<CR>
:    nnoremap <buffer> 6        :call VffText('6')<CR>
:    nnoremap <buffer> 7        :call VffText('7')<CR>
:    nnoremap <buffer> 8        :call VffText('8')<CR>
:    nnoremap <buffer> 9        :call VffText('9')<CR>
:    nnoremap <buffer> `        :call VffText('`')<CR>
:    nnoremap <buffer> :        :call VffText(':')<CR>
:    nnoremap <buffer> .        :call VffText('.')<CR>
:    nnoremap <buffer> ,        :call VffText(',')<CR>
:    nnoremap <buffer> ?        :call VffText('?')<CR>
:    nnoremap <buffer> <        :call VffText('<')<CR>
:    nnoremap <buffer> >        :call VffText('>')<CR>
:    nnoremap <buffer> /        :call VffText('/')<CR>
:    nnoremap <buffer> \        :call VffText('\\')<CR>
:    nnoremap <buffer> !        :call VffText('!')<CR>
:    nnoremap <buffer> @        :call VffText('@')<CR>
:    nnoremap <buffer> #        :call VffText('#')<CR>
:    nnoremap <buffer> $        :call VffText('$')<CR>
:    nnoremap <buffer> %        :call VffText('%')<CR>
:    nnoremap <buffer> ^        :call VffText('^')<CR>
:    nnoremap <buffer> &        :call VffText('&')<CR>
:    nnoremap <buffer> *        :call VffText('*')<CR>
:    nnoremap <buffer> (        :call VffText('(')<CR>
:    nnoremap <buffer> )        :call VffText(')')<CR>
:    nnoremap <buffer> [        :call VffText('[')<CR>
:    nnoremap <buffer> {        :call VffText('{')<CR>
:    nnoremap <buffer> ]        :call VffText(']')<CR>
:    nnoremap <buffer> }        :call VffText('}')<CR>
:    nnoremap <buffer> -        :call VffText('-')<CR>
:    nnoremap <buffer> _        :call VffText('_')<CR>
:    nnoremap <buffer> +        :call VffText('+')<CR>
:    nnoremap <buffer> =        :call VffText('=')<CR>
:    nnoremap <buffer> "        :call VffText('"')<CR>
:    nnoremap <buffer> ~        :call VffText('~')<CR>
:    nnoremap <buffer> '        :call VffText('\''')<CR>
:    nnoremap <buffer> <C-U>    :call VffClear()<CR>
:    nnoremap <buffer> <BS>     :call VffBackspace()<CR>
:    nnoremap <buffer> <M-J>    :call VffDown(1)<CR>
:    nnoremap <buffer> <M-K>    :call VffUp(1)<CR>
:    nnoremap <buffer> <A-J>    :call VffDown(1)<CR>
:    nnoremap <buffer> <A-K>    :call VffUp(1)<CR>
:    nnoremap <buffer> ∆        :call VffDown(1)<CR>
:    nnoremap <buffer> ˚        :call VffUp(1)<CR>
:    nnoremap <buffer> j      :call VffDown(1)<CR>
:    nnoremap <buffer> k      :call VffUp(1)<CR>
:    nnoremap <buffer> <C-DOWN> :call VffDown(1)<CR>
:    nnoremap <buffer> <C-UP>   :call VffUp(1)<CR>
:    nnoremap <buffer> <A-DOWN> :call VffDown(1)<CR>
:    nnoremap <buffer> <A-UP>   :call VffUp(1)<CR>
:    nnoremap <buffer> <S-DOWN> :call VffDown(1)<CR>
:    nnoremap <buffer> <S-UP>   :call VffUp(1)<CR>
:    nnoremap <buffer> <C-J>    :call VffDown(1)<CR>
:    nnoremap <buffer> <C-K>    :call VffUp(1)<CR>
:    nnoremap <buffer> <DOWN>   :call VffDown(1)<CR>
:    nnoremap <buffer> <UP>     :call VffUp(1)<CR>
:    cabbr <buffer> w q
:    cabbr <buffer> wq q
:    call VffSetupDeActivationKey ()
:    let g:VffSetup = 1
:  endif
:endfunction

:ruby << EOF
    require 'socket'
    require 'pathname'
    class VFF
        def initialize()
            @foundvff = false
            @findtext = ""
            @greptext = ""

            pn = Pathname.pwd
            while (!pn.root?)
                tpn = pn + ".vff"
                if (tpn.exist?)
                    @foundvff = true
                    @vffpath = pn + ".vff"
                    @path = pn
                    return true
                end
                pn = pn + ".."
                pn = Pathname.new(pn.cleanpath())
            end
        end
        def enter(mode)
            if (mode == 'find')
                @findtext = ''
            end

            buffer = VIM::Buffer.current
            if (@foundvff)

                connect()

                while (buffer.count > 1)
                VIM::command(":  echo '" + buffer.count + "'")
                buffer.delete(1)
                end

                buffer.append(0, "VimFastFind: Ctrl-F for file mode, Ctrl-E for grep mode");
                buffer.append(1, "<ESC> to quit, <UP>/<DOWN> or Alt-J/Alt-K to move, <ENTER> to select");
                buffer.append(2, "----------------------------------------------------------------------");

                buffer.append(3, "Root: " + @path.to_s())
                buffer.append(4, "")
                if (mode == 'find')
                    buffer.append(5, "Find File: ")
                else
                    buffer.append(5, "Find Content: ")
                end
                buffer.append(6, "")
                buffer.append(7, "")
                while (buffer.count >= 8)
                    buffer.delete(8)
                end
                VIM::command(":  aug ListFiles")
                VIM::command(":    exec \"au WinEnter \" . g:vffWindowName . \" call VffSetupSelect ()\"")
                VIM::command(":    exec \"au WinLeave \" . g:vffWindowName . \" call VffUnsetupSelect ()\"")
                VIM::command(":    exec \"au BufLeave \" . g:vffWindowName . \" call VffClearSetup ()\"")
                VIM::command(":  aug END")
                VIM::command(":  setlocal cursorline")
                VIM::command(":  normal G")
                VIM::command(":  call VffSetupSelect ()")

                if (mode == 'grep')
                    _refresh(mode, true)
                end

            else
                VIM::command(":  call VffSetupBadSelect ()")
                hook=<<EOS
ERROR: No .vff file found!

Hit ESCAPE or ENTER to close this window

In the root of the filesystem tree you want to scan, create a .vff file.

After that, you can include or exclude files using the following statements:

[file|grep] include <pattern>
[file|grep] exclude <pattern>

You can include/exclude for just find mode or just grep mode by prefixing the
include/exclude statement with "file" or "grep". Not specifying "file" or
"grep" will cause the include/exclude to match for both.

Patterns are matched in order and short circuit on match. Unmatched files will
be excluded.

"#" is the start of a comment on any line. Blank lines are ignored.


Example:

% cat .vff
include *.c
include *.cs
include *.cpp
include *.h
include *.java
include *.lua
include *.pl
include *.py
include *.rb
include *.tcl
include *.awk
include *.sed
include *.sh
include *.bash



EOS
                i = 0
                for l in hook.split("\n")
                    buffer.append(i, l)
                    i += 1
                end
            end
        end

        def connect()
            if (!@foundvff)
                return false
            end

            begin
                if (@sock)
                    @sock.puts('nop')
                    @sock.gets
                end
            rescue
                @sock = nil
            end

            if (!@sock)
                i = 0
                begin
                    connect2()
                rescue
                    job = fork do
                        if (RUBY_PLATFORM == 'i386-cygwin' or RUBY_PLATFORM == 'x86_64-cygwin')
                            exec ENV['HOME'] + "/.vim/plugin/VFF/VFFServer.exe"
                        else
                            exec "mono " + ENV['HOME'] + "/.vim/plugin/VFF/VFFServer.exe"
                        end
                    end
                    Process.detach(job)

                    i = 0
                    while (i < 50)
                        sleep(0.0100)
                        begin
                            connect2()
                            i = 99
                        rescue
                        end
                    end
                end
            end
        end

        def connect2()
            @sock = TCPSocket.open("127.0.0.1", 20398)
            f = File.new(@vffpath)
            @sock.puts('init ' + @path.to_s())
            while (true)
                s = f.gets
                if (s == nil)
                    break
                end
                @sock.puts("config " + s)
            end
            @sock.puts('go')
        end

        def text_append(mode, s)
            if (mode == 'find')
                @findtext += s
            else
                @greptext += s
            end
            # don't update results until refresh is called
            _refresh(mode, false)
        end

        def text_backspace(mode)
            if (mode == 'find')
                @findtext.chop!()
            else
                @greptext.chop!()
            end
            # don't update results until refresh is called
            _refresh(mode, false)
        end

        def text_clear(mode)
            if (mode == 'find')
                @findtext = ''
            else
                @greptext = ''
            end
            # update results immediately
            _refresh(mode, true)
        end

        def refresh(mode)
            _refresh(mode, true)
        end

        def _refresh(mode, domatching)
            _refresh2(mode, domatching, true)
        end

        def _refresh2(mode, domatching, doretry)
            if (!@foundvff)
                return false
            end
            buffer = VIM::Buffer.current
            buffer.delete(6)
            if (mode == 'find')
                buffer.append(5, "Find File: " + @findtext)
                text = @findtext
            else
                buffer.append(5, "Find Content: " + @greptext)
                text = @greptext
            end
            while (buffer.count >= 7)
                buffer.delete(7)
            end
            if domatching
                connect()
                begin
                    if ((mode == "find" && text != "") || (mode == "grep" && text.length >= 3))
                        if (mode == 'find')
                            @sock.puts("find match " + text)
                        else
                            @sock.puts("grep match " + text)
                        end
                        while line = @sock.gets
                            line = line.gsub(/\r\n?/, "\n").chop
                            if (line == "")
                                break
                            end
                            buffer.append(buffer.count, line)
                        end
                        if (line == nil && doretry)
                            connect()
                            _refresh2(mode, false)
                        end
                    end
                rescue
                    if (doretry)
                        connect()
                        _refresh2(mode, false)
                    end
                end
            end

            buffer.append(buffer.count, "")
            VIM::command("set nomodified")
        end
        def relativepath(relativeto,abspath)
            abspath = @path.to_s() + abspath
            path = abspath.split("/")
            rel = relativeto.split("/")
            while (path.length > 0) && (path.first == rel.first)
                path.shift
                rel.shift
            end
            VIM::command("let g:vffrubyret = \"" + (('..' + "/") * (rel.length) + path.join("/")) + "\"")
        end
    end
    $vff = VFF.new()
EOF

:if exists("g:vff_refreshdelay")
:  exec "set updatetime=" . g:vff_refreshdelay
:  " this autocommand fires when a char hasn't been typed in 'updatetime' ms, in normal mode
:  autocmd CursorHold * :call VffRefresh()
:endif

:function! VffRefresh ()
:  if exists("g:vff_needrefresh")
:    if exists("g:vff_refreshdelay")
:      exec "ruby $vff.refresh('" . g:vff_mode . "')"
:    endif
:    unlet g:vff_needrefresh
:  endif
:endfunction

" updates the entry line immediately but don't refresh the results until the next CursorHold event
:function! VffText (ch)
:  exec "ruby $vff.text_append('" . g:vff_mode . "' , '" . a:ch . "')"
:  let g:vff_lastline = line(".")
:  echo ""
:  if exists("g:vff_refreshdelay")
:    let g:vff_needrefresh = 1
:  else
:    exec "ruby $vff.refresh('" . g:vff_mode . "')"
:  endif
:endfunction

" updates the entry line immediately but don't refresh the results until the next CursorHold event
:function! VffBackspace ()
:  exec "ruby $vff.text_backspace('" . g:vff_mode . "')"
:  let g:vff_lastline = line(".")
:  echo ""
:  if exists("g:vff_refreshdelay")
:    let g:vff_needrefresh = 1
:  else
:    exec "ruby $vff.refresh('" . g:vff_mode . "')"
:  endif
:endfunction

" updates the entry and results immediately
:function! VffClear ()
:  exec "ruby $vff.text_clear('" . g:vff_mode . "')"
:  let g:vff_lastline = line(".")
:  echo ""
:endfunction

:function! VffUp(v)
:  let l:line = line(".")
:  if l:line - a:v > 7
:    silent! exec "normal! " . a:v . "k"
:  else
:    7
:  endif
:  let g:vff_lastline = line(".")
:  echo ""
:endfunction

:function! VffDown(v)
:  silent! exec "normal! " . a:v . "j"
:  let g:vff_lastline = line(".")
:  echo ""
:endfunction

:function! VffUnsetupSelect ()
:  if exists ("g:VffSetup")
:    call VffSetupActivationKey ()
:    unlet g:VffSetup
:  endif
:endfunction

:function! VffSelectCurrentBuffer ()
:  let &timeoutlen = g:vff_savetimeoutlen
:  let l:myBufNr = bufnr ("%")
:  let l:line = getline(".")
:  quit
:  if l:line != ""
:    exec 'ruby $vff.relativepath("' . getcwd() . '", "/' . substitute(l:line, "([0-9]\\+):.*", "", "") . '")'
:    silent exec "edit " . fnameescape(g:vffrubyret)
:    if g:vff_mode == 'grep'
:      let l:offset = substitute(l:line, "^[^(]*(\\([0-9]\\+\\)):.*", "\\1", "")
:      exec 'goto ' . l:offset
:    endif
:  endif
:  if g:vffRemoveBrowserBuffer
:    silent! exec "bd " . l:myBufNr
:  endif
:endfunction

:function! VffQuit ()
:  let &timeoutlen = g:vff_savetimeoutlen
:  let l:myBufNr = bufnr ("%")
:  silent! exec "bd " . l:myBufNr
:  call VffUnsetupSelect()
:endfunction
